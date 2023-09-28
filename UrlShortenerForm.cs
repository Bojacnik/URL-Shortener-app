using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Newtonsoft.Json;
using WindowsFormsApp2.Failures;

namespace WindowsFormsApp2
{
  public class UrlShortenerForm : Form
  {
    private const string PostUri = "https://cleanuri.com/api/v1/shorten";
    private TextBox textBox1;
    private TextBox textBox2;
    private Button button1;
    private Label label1;

    public UrlShortenerForm() => InitializeComponent();

    private static async Task<OneOf.OneOf<NetworkFailure, string>> FetchShortenedUrl(string url)
    {
      var content = new StringContent("url=" + HttpUtility.UrlEncode(url));
      content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
      using (var httpClient = new HttpClient())
      {
        try
        {
          var httpResponseMessage = await httpClient.PostAsync(PostUri, content);
          if (!httpResponseMessage.IsSuccessStatusCode)
            return new PostFailure("Post request resulted in status: " + httpResponseMessage.StatusCode);

          var urlJson = await httpResponseMessage.Content.ReadAsStringAsync();
          var deserializedUrl = JsonConvert.DeserializeObject<UrlDto>(urlJson);
          return deserializedUrl.result_url;
        }
        catch (Exception ex)
        {
          return new FetchFailure(ex.Message);
        }
      }
    }

    private async void button1_Click(object sender, EventArgs e)
    {
      var result = await FetchShortenedUrl(textBox1.Text);
      result.Switch(failure => MessageBox.Show(failure.Message), url => textBox2.Text = url);
    }

    protected override void Dispose(bool disposing)
    {
      textBox1.Dispose();
      textBox2.Dispose();
      button1.Dispose();
      label1.Dispose();
    }

    private void InitializeComponent()
    {
      textBox1 = new TextBox();
      button1 = new Button();
      label1 = new Label();
      textBox2 = new TextBox();
      SuspendLayout();
      textBox1.Location = new Point(12, 12);
      textBox1.Name = "textBox1";
      textBox1.Size = new Size(285, 22);
      textBox1.TabIndex = 0;
      button1.Location = new Point(12, 48);
      button1.Name = "button1";
      button1.Size = new Size(284, 41);
      button1.TabIndex = 1;
      button1.Text = "shorten";
      button1.UseVisualStyleBackColor = true;
      button1.Click += button1_Click;
      label1.Location = new Point(12, 106);
      label1.Name = "label1";
      label1.Size = new Size(285, 41);
      label1.TabIndex = 2;
      label1.Text = "Waiting for URI";
      label1.TextAlign = ContentAlignment.MiddleCenter;
      textBox2.Location = new Point(12, 160);
      textBox2.Name = "textBox2";
      textBox2.Size = new Size(285, 22);
      textBox2.TabIndex = 3;
      AutoScaleDimensions = new SizeF(8f, 16f);
      AutoScaleMode = AutoScaleMode.Font;
      BackgroundImageLayout = ImageLayout.None;
      ClientSize = new Size(308, 208);
      Controls.Add(textBox2);
      Controls.Add(label1);
      Controls.Add(button1);
      Controls.Add(textBox1);
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
