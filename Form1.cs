// Decompiled with JetBrains decompiler
// Type: WindowsFormsApp2.Form1
// Assembly: WindowsFormsApp2, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6013BA42-02F4-4668-A0E9-E34B7235D0BC
// Assembly location: C:\Users\bojac\Downloads\WindowsFormsApp2.exe

using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using WindowsFormsApp2.Failures;

namespace WindowsFormsApp2
{
  public class Form1 : Form
  {
    private const string PostUri = "https://cleanuri.com/api/v1/shorten";
    private IContainer components;
    private TextBox textBox2;
    private TextBox textBox1;
    private Button button1;
    private Label label1;

    public Form1() => this.InitializeComponent();

    private async Task<OneOf.OneOf<NetworkFailure, string>> fetchShortenedUrl(string url)
    {
      StringContent content = new StringContent("url=" + HttpUtility.UrlEncode(url));
      content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
      using (HttpClient httpClient = new HttpClient())
      {
        try
        {
          HttpResponseMessage httpResponseMessage = await httpClient.PostAsync("https://cleanuri.com/api/v1/shorten", (HttpContent) content);
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
      var result = await fetchShortenedUrl(textBox1.Text);
      result.Switch(failure => MessageBox.Show(failure.Message), url => textBox2.Text = url);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.textBox1 = new TextBox();
      this.button1 = new Button();
      this.label1 = new Label();
      this.textBox2 = new TextBox();
      this.SuspendLayout();
      this.textBox1.Location = new Point(12, 12);
      this.textBox1.Name = "textBox1";
      this.textBox1.Size = new Size(285, 22);
      this.textBox1.TabIndex = 0;
      this.button1.Location = new Point(12, 48);
      this.button1.Name = "button1";
      this.button1.Size = new Size(284, 41);
      this.button1.TabIndex = 1;
      this.button1.Text = "shorten";
      this.button1.UseVisualStyleBackColor = true;
      this.button1.Click += new EventHandler(this.button1_Click);
      this.label1.Location = new Point(12, 106);
      this.label1.Name = "label1";
      this.label1.Size = new Size(285, 41);
      this.label1.TabIndex = 2;
      this.label1.Text = "Waiting for URI";
      this.label1.TextAlign = ContentAlignment.MiddleCenter;
      this.textBox2.Location = new Point(12, 160);
      this.textBox2.Name = "textBox2";
      this.textBox2.Size = new Size(285, 22);
      this.textBox2.TabIndex = 3;
      this.AutoScaleDimensions = new SizeF(8f, 16f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.BackgroundImageLayout = ImageLayout.None;
      this.ClientSize = new Size(308, 208);
      this.Controls.Add((Control) this.textBox2);
      this.Controls.Add((Control) this.label1);
      this.Controls.Add((Control) this.button1);
      this.Controls.Add((Control) this.textBox1);
      this.FormBorderStyle = FormBorderStyle.FixedSingle;
      this.MaximizeBox = false;
      this.Name = nameof (Form1);
      this.ShowIcon = false;
      this.Text = "URL Shortener";
      this.ResumeLayout(false);
      this.PerformLayout();
    }
  }
}
