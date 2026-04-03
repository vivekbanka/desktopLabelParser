using bpac;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace labelParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public class AamvaLicense
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string MiddleName { get; set; }
            public string LicenseNumber { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public DateTime? IssueDate { get; set; }
            public string Street { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
            public string Sex { get; set; }
            public string EyeColor { get; set; }
            public string HairColor { get; set; }
            public string Height { get; set; }
            public string VehicleClass { get; set; }
            public string Restrictions { get; set; }
            public Dictionary<string, string> AllFields { get; set; } = new();

            public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.Today;
        }
        public MainWindow()
        {
            InitializeComponent();
            ScanBox.Focus();
        }
        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            int age = today.Year - dob.Year;
            if (dob.AddYears(age) > today)
                age--;
            return age;
        }

        private void ScanBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var data = ScanBox.Text;

            if (data.Contains("@ANSI") && data.Contains('=')) {
                AamvaLicense aamvaLicense =  Parse(data);
                var name = aamvaLicense.FirstName + " " + aamvaLicense.LastName;
                //var dob = GetField(data, "DBB");
                //var age = CalculateAge(dob);

                NameText.Text = name.Trim();
                int age = CalculateAge(aamvaLicense.DateOfBirth.Value);
                AgeText.Text = age.ToString();
                DOBText.Text = aamvaLicense.DateOfBirth?.ToString("%d-MMM-yyyy").ToUpper() ?? "N/A";
                //AgeText.Text = age.ToString();

                //PrintLabel(name, age.ToString());

                ScanBox.Clear();
            }
        }

        public static AamvaLicense Parse(string barcodeData)
        {
            if (string.IsNullOrWhiteSpace(barcodeData))
                throw new ArgumentException("Barcode data is empty.");

            // Validate ANSI header
            if (!barcodeData.Contains("ANSI "))
                throw new FormatException("Not a valid AAMVA barcode (missing ANSI header).");

            // Find the DL subfile
            int dlIndex = barcodeData.IndexOf("DL", StringComparison.Ordinal);
            if (dlIndex < 0)
                throw new FormatException("DL subfile not found.");

            string subfile = barcodeData[dlIndex..];

            // Parse all 3-letter field codes followed by their value (up to next code or newline)
            var fields = new Dictionary<string, string>();
         //Tighter pattern: code is always D + 2 uppercase letters (DA_, DB_, DC_, DD_, DZ_)
            var matches = Regex.Matches(subfile, @"(D[A-Z]{2})(.*?)(?=D[A-Z]{2}|$)", RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                string code = m.Groups[1].Value;
                string value = m.Groups[2].Value.Trim();
                if (code != "DLD" && !string.IsNullOrEmpty(value))
                    fields.TryAdd(code, value);
            }

            var lic = new AamvaLicense { AllFields = fields };
            lic.FirstName = fields.TryGetValue("DAC", out var first) ? first : null;
            lic.LastName = fields.TryGetValue("DCS", out var last) ? last : null;
            lic.MiddleName = fields.TryGetValue("DAD", out var mid) && mid != "NONE" ? mid : null;

            lic.LicenseNumber = fields.TryGetValue("DAQ", out var licNum) ? licNum : null;
            lic.Street = fields.TryGetValue("DAG", out var street) ? street : null;
            lic.City = fields.TryGetValue("DAI", out var city) ? city : null;
            lic.State = fields.TryGetValue("DAJ", out var state) ? state : null;
            lic.Zip = fields.TryGetValue("DAK", out var zip) ? zip?[..5] : null;
            lic.EyeColor = fields.TryGetValue("DAY", out var eye) ? eye : null;
            lic.HairColor = fields.TryGetValue("DAZ", out var hair) ? hair : null;
            lic.Height = fields.TryGetValue("DAU", out var height) ? height : null;
            lic.VehicleClass = fields.TryGetValue("DCA", out var vClass) ? vClass : null;
            lic.Restrictions = fields.TryGetValue("DCB", out var restr) ? restr : null;

            lic.Sex = fields.TryGetValue("DBC", out var sex)
                ? sex == "1" ? "Male" : sex == "2" ? "Female" : sex
                : null;

            lic.DateOfBirth = fields.TryGetValue("DBB", out var dob) ? ParseDate(dob) : null;
            lic.ExpirationDate = fields.TryGetValue("DBA", out var exp) ? ParseDate(exp) : null;
            lic.IssueDate = fields.TryGetValue("DBD", out var iss) ? ParseDate(iss) : null;

            return lic;
        }
        private string GetField(string data, string code)
        {
            var match = Regex.Match(data, code + @"([^\r\n]+)");
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static DateTime? ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8) return null;
            return DateTime.TryParseExact(raw[..8], "MMddyyyy",
                null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        }

        private void PrintLabel(string name, string age)
        {
            try
            {
                var doc = new Document();
                doc.Open(@"C:\labels\label.lbx");

                doc.GetObject("Name").Text = name;
                doc.GetObject("Age").Text = age;

                doc.StartPrint("", PrintOptionConstants.bpoDefault);
                doc.PrintOut(1, PrintOptionConstants.bpoDefault);
                doc.EndPrint();

                StatusText.Text = "✅ Printed";
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ " + ex.Message;
            }
        }
    }
}