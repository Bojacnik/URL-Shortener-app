using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Newtonsoft.Json;
using UrlShortener.Failures;

namespace UrlShortener
{
  public class UrlShortenerForm : Form
  {
    private const string PostUri = "https://cleanuri.com/api/v1/shorten/url?url=www.youtube.com";
    private TextBox _textBox1;
    private TextBox _textBox2;
    private Button _button1;
    private Label _label1;

    public UrlShortenerForm() => InitializeComponent();

    private static async Task<OneOf.OneOf<NetworkFailure, string>> FetchShortenedUrl(string url)
    {
      url = url.Trim();
      var values = new[]
      {
        new KeyValuePair<string, string>("url", url)
      };

      var httpClient = new HttpClient();
      httpClient.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");

      var content = new FormUrlEncodedContent(values);
      try
      {
        HttpResponseMessage response = await httpClient.PostAsync(PostUri, content);
        httpClient.Dispose();
        if (!response.IsSuccessStatusCode)
          return new PostFailure($"Status: {response.StatusCode}");

        string json = await response.Content.ReadAsStringAsync();
        var dto = JsonConvert.DeserializeObject<UrlDto>(json);
        return dto.ResultUrl;
      }
      catch (Exception ex)
      {
        return new FetchFailure(ex.Message);
      }
    }


    private async void button1_Click(object sender, EventArgs e)
    {
      var result = await FetchShortenedUrl(_textBox1.Text);
      result.Switch(failure => MessageBox.Show(failure.Message), url => _textBox2.Text = url);
    }

    protected override void Dispose(bool disposing)
    {
      _textBox1.Dispose();
      _textBox2.Dispose();
      _button1.Dispose();
      _label1.Dispose();
    }

    private void InitializeComponent()
    {
      _textBox1 = new TextBox();
      _button1 = new Button();
      _label1 = new Label();
      _textBox2 = new TextBox();
      SuspendLayout();
      _textBox1.Location = new Point(12, 12);
      _textBox1.Name = "_textBox1";
      _textBox1.Size = new Size(285, 22);
      _textBox1.TabIndex = 0;
      _button1.Location = new Point(12, 48);
      _button1.Name = "_button1";
      _button1.Size = new Size(284, 41);
      _button1.TabIndex = 1;
      _button1.Text = "shorten";
      _button1.UseVisualStyleBackColor = true;
      _button1.Click += button1_Click;
      _label1.Location = new Point(12, 106);
      _label1.Name = "_label1";
      _label1.Size = new Size(285, 41);
      _label1.TabIndex = 2;
      _label1.Text = "Waiting for URI";
      _label1.TextAlign = ContentAlignment.MiddleCenter;
      _textBox2.Location = new Point(12, 160);
      _textBox2.Name = "_textBox2";
      _textBox2.Size = new Size(285, 22);
      _textBox2.TabIndex = 3;
      AutoScaleDimensions = new SizeF(8f, 16f);
      AutoScaleMode = AutoScaleMode.Font;
      BackgroundImageLayout = ImageLayout.None;
      ClientSize = new Size(308, 208);
      Controls.Add(_textBox2);
      Controls.Add(_label1);
      Controls.Add(_button1);
      Controls.Add(_textBox1);
      FormBorderStyle = FormBorderStyle.FixedSingle;
      MaximizeBox = false;
      Name = nameof (UrlShortenerForm);
      ShowIcon = false;
      Text = "URL Shortener";
      ResumeLayout(false);
      PerformLayout();
    }
  }
}
