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
        private const string TemplatePath = @"C:\Users\banka\source\repos\desktopLabelParser\labelParser\tube_label.lbx";

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

            pd.PrintPage += (sender, e) =>
            {
                var g = e.Graphics!;
                g.Clear(SysColor.White);

                int margin    = 10;
                int y         = 15;
                int pageWidth = (int)e.PageBounds.Width;
                int drawWidth = pageWidth - margin * 2;

                // Patient name
                using var nameFont = new SysFont("Arial", 14f, SysFontStyle.Bold);
                g.DrawString(
                    $"{license.FirstName} {license.LastName}",
                    nameFont, SysBrushes.Black,
                    new SysRectF(margin, y, drawWidth, 50));
                y += 48;

                // Divider
                g.DrawLine(SysPens.Black, margin, y, pageWidth - margin, y);
                y += 8;

                using var lblFont = new SysFont("Arial", 7f);
                using var valFont = new SysFont("Arial", 10f, SysFontStyle.Bold);

                // DOB
                g.DrawString("DATE OF BIRTH", lblFont, SysBrushes.Gray,
                    new SysRectF(margin, y, drawWidth, 16));
                y += 15;
                g.DrawString(
                    license.DateOfBirthFormatted ?? "N/A",
                    valFont, SysBrushes.Black,
                    new SysRectF(margin, y, drawWidth, 24));
                y += 26;

                // Divider
                g.DrawLine(SysPens.Black, margin, y, pageWidth - margin, y);
                y += 8;

                // Printed datetime
                g.DrawString("PRINTED", lblFont, SysBrushes.Gray,
                    new SysRectF(margin, y, drawWidth, 16));
                y += 15;
                g.DrawString(
                    DateTime.Now.ToString("dd-MMM-yyyy HH:mm").ToUpper(),
                    valFont, SysBrushes.Black,
                    new SysRectF(margin, y, drawWidth, 24));
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
