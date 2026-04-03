// Explicit aliases to avoid WPF namespace conflicts with System.Drawing
using SysFont = System.Drawing.Font;
using SysBrushes = System.Drawing.Brushes;
using SysPens = System.Drawing.Pens;
using SysColor = System.Drawing.Color;
using SysBitmap = System.Drawing.Bitmap;
using SysRectF = System.Drawing.RectangleF;
using SysFontStyle = System.Drawing.FontStyle;
using SysGraphics = System.Drawing.Graphics;

using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace labelParser
{
    public partial class MainWindow : Window
    {
        private static readonly string TemplatePath = Path.Combine(AppContext.BaseDirectory, "tube_label.lbx");

        public class AamvaLicense
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? MiddleName { get; set; }
            public string? LicenseNumber { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string? DateOfBirthFormatted { get; set; }
            public int? Age { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public DateTime? IssueDate { get; set; }
            public string? Street { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? Zip { get; set; }
            public string? Sex { get; set; }
            public string? EyeColor { get; set; }
            public string? HairColor { get; set; }
            public string? Height { get; set; }
            public string? VehicleClass { get; set; }
            public string? Restrictions { get; set; }
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
            if (dob.AddYears(age) > today) age--;
            return age;
        }

        private static DateTime? ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8) return null;
            return DateTime.TryParseExact(raw[..8], "MMddyyyy",
                null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        }

        public static AamvaLicense Parse(string barcodeData)
        {
            if (string.IsNullOrWhiteSpace(barcodeData))
                throw new ArgumentException("Barcode data is empty.");

            if (!barcodeData.Contains("ANSI "))
                throw new FormatException("Not a valid AAMVA barcode (missing ANSI header).");

            // Find DL subfile — must be followed immediately by a D[A-Z]{2} field code
            var dlMatch = Regex.Match(barcodeData, @"DL(?=D[A-Z]{2})");
            if (!dlMatch.Success)
                throw new FormatException("DL subfile not found.");

            string subfile = barcodeData[dlMatch.Index..];

            var fields = new Dictionary<string, string>();
            var matches = Regex.Matches(subfile, @"(D[A-Z]{2})(.*?)(?=D[A-Z]{2}|$)", RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                string code = m.Groups[1].Value;
                string value = m.Groups[2].Value.Trim();
                if (code != "DLD" && !string.IsNullOrEmpty(value))
                    fields.TryAdd(code, value);
            }

            var lic = new AamvaLicense { AllFields = fields };

            lic.FirstName    = fields.TryGetValue("DAC", out var first) ? first : null;
            lic.LastName     = fields.TryGetValue("DCS", out var last)  ? last  : null;
            lic.MiddleName   = fields.TryGetValue("DAD", out var mid) && mid != "NONE" ? mid : null;
            lic.LicenseNumber = fields.TryGetValue("DAQ", out var licNum) ? licNum : null;
            lic.Street       = fields.TryGetValue("DAG", out var street) ? street : null;
            lic.City         = fields.TryGetValue("DAI", out var city)   ? city   : null;
            lic.State        = fields.TryGetValue("DAJ", out var state)  ? state  : null;
            lic.Zip          = fields.TryGetValue("DAK", out var zip)    ? zip?[..5] : null;
            lic.EyeColor     = fields.TryGetValue("DAY", out var eye)    ? eye    : null;
            lic.HairColor    = fields.TryGetValue("DAZ", out var hair)   ? hair   : null;
            lic.Height       = fields.TryGetValue("DAU", out var height) ? height : null;
            lic.VehicleClass = fields.TryGetValue("DCA", out var vClass) ? vClass : null;
            lic.Restrictions = fields.TryGetValue("DCB", out var restr)  ? restr  : null;

            lic.Sex = fields.TryGetValue("DBC", out var sex)
                ? sex == "1" ? "Male" : sex == "2" ? "Female" : sex
                : null;

            lic.DateOfBirth          = fields.TryGetValue("DBB", out var dob) ? ParseDate(dob) : null;
            lic.DateOfBirthFormatted = lic.DateOfBirth?.ToString("%d-MMM-yyyy").ToUpper();
            lic.Age                  = lic.DateOfBirth.HasValue ? CalculateAge(lic.DateOfBirth.Value) : null;
            lic.ExpirationDate       = fields.TryGetValue("DBA", out var exp) ? ParseDate(exp) : null;
            lic.IssueDate            = fields.TryGetValue("DBD", out var iss) ? ParseDate(iss) : null;

            return lic;
        }

        public static void PrintTubeLabel(AamvaLicense license)
        {
            var pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = "Brother QL-710W";

            // Swap dimensions: 60mm wide x 29mm tall (landscape/rotated)
            pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", 236, 114);
            pd.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
            pd.DefaultPageSettings.Landscape = true;

            pd.PrintPage += (sender, e) =>
            {
                var g = e.Graphics!;
                g.PageUnit = System.Drawing.GraphicsUnit.Millimeter;
                g.Clear(SysColor.White);

                // Rotate 90 degrees so text prints vertically along the tube
                g.TranslateTransform(0, 29f);
                g.RotateTransform(-90f);

                // Now drawing as if on 29mm wide x 60mm tall canvas
                float mL = 1.5f;
                float mR = 1.5f;
                float y = 2f;
                float w = 60f - mL - mR; // full length of tube is now our width

                string line = $"{license.FirstName} {license.LastName}  /  {license.DateOfBirthFormatted ?? "N/A"}";

                using var font = new SysFont("Arial", 3.8f, SysFontStyle.Bold);
                g.DrawString(line, font, SysBrushes.Black, new SysRectF(mL, y, w, 8f));

                e.HasMorePages = false;
            };

            pd.Print();
        }
        private void ScanBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var data = ScanBox.Text;

            if (data.Contains("@ANSI") && data.Contains('='))
            {
                try
                {
                    AamvaLicense license = Parse(data);

                    NameText.Text = $"{license.FirstName} {license.LastName}".Trim();
                    AgeText.Text  = license.Age?.ToString() ?? "N/A";
                    DOBText.Text  = license.DateOfBirthFormatted ?? "N/A";

                    PrintTubeLabel(license);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Parse Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ScanBox.Clear();
                }
            }
        }
    }
}
