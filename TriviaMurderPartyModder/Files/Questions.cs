using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using TriviaMurderPartyModder.Data;

namespace TriviaMurderPartyModder.Files {
    public class Questions : DataFile<Question> {
        protected override string ReferenceFileName => "TDQuestion.jet";

        public Questions() : base("questions") { }

        /// <summary>
        /// Shuffle the choices to make them equally likely to be the correct answer.
        /// </summary>
        public void Equalize() {
            bool wasChange = false;
            for (int i = 0, c = Count; i < c; i++) {
                int shouldBeCorrect = i % 4 + 1;
                Question item = Items[i];
                if (item.Correct != shouldBeCorrect) {
                    (item[item.Correct], item[shouldBeCorrect]) = (item[shouldBeCorrect], item[item.Correct]);
                    item.Correct = shouldBeCorrect;
                    wasChange = true;
                }
            }

            // OnCollectionChanged
            if (wasChange) {
                Replace([.. this]);
            }
        }

        /// <summary>
        /// Load all questions from a file and append them to the existing list of questions.
        /// </summary>
        protected override void Add(string fileName) {
            using JsonDocument source = JsonDocument.Parse(File.OpenRead(fileName));
            var root = source.RootElement.GetProperty("content");
            foreach (JsonElement question in root.EnumerateArray()) {
                JsonElement id = question.GetProperty("id");
                Question imported = new() {
                    ID = id.ValueKind == JsonValueKind.Number ? id.GetInt32() : int.Parse(id.GetString()),
                    Text = question.GetProperty("text").GetString()
                };

                JsonElement choicesArray = question.GetProperty("choices");
                int answer = 0;
                foreach (JsonElement choice in choicesArray.EnumerateArray()) {
                    answer++;
                    imported[answer] = choice.GetProperty("text").GetString();
                    if (choice.TryGetProperty("correct", out JsonElement correct) && correct.GetBoolean()) {
                        imported.Correct = answer;
                    }
                }

                Add(imported);
            }
        }

        protected override bool SaveAs(string name) {
            Question[] ordered = [.. this.OrderBy(x => x.ID)];
            Clear();
            for (int i = 0; i < ordered.Length; i++) {
                Add(ordered[i]);
            }

            StringBuilder output = new("{\"episodeid\":1244,\"content\":[");
            for (int i = 0, end = Count; i < end; i++) {
                Question q = this[i];
                output.Append("{\"x\":false,\"id\":").Append(q.ID);
                if (string.IsNullOrWhiteSpace(q.Text)) {
                    Issue(string.Format("No text given for question ID {0}.", q.ID));
                    return false;
                }
                output.Append(",\"text\":\"").Append(Parsing.MakeTextCompatible(q.Text)).Append("\",\"pic\": false,\"choices\":[");
                if (q.Correct < 1 || q.Correct > 4) {
                    Issue(string.Format("No correct answer set for question \"{0}\".", q.Text));
                    return false;
                }
                for (int answer = 1; answer <= 4; answer++) {
                    if (answer != 1)
                        output.Append("},{");
                    else
                        output.Append('{');
                    if (answer == q.Correct)
                        output.Append("\"correct\":true,");
                    if (string.IsNullOrWhiteSpace(q[answer])) {
                        Issue(string.Format("No answer {0} for question \"{1}\".", answer, q.Text));
                        return false;
                    }
                    output.Append("\"text\":\"").Append(Parsing.MakeTextCompatible(q[answer])).Append('"');
                }
                if (i != end - 1)
                    output.Append("}]},");
                else
                    output.Append("}]}]}");
            }
            File.WriteAllText(name, output.ToString());
            return true;
        }

        /// <summary>
        /// Replace the contents of this <see cref="Question"/> database with new <paramref name="questions"/>.
        /// </summary>
        void Replace(Question[] questions) {
            Clear();
            for (int i = 0; i < questions.Length; i++) {
                Add(questions[i]);
            }
        }
    }
}