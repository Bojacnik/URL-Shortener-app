// Decompiled with JetBrains decompiler
// Type: WindowsFormsApp2.Program
// Assembly: WindowsFormsApp2, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6013BA42-02F4-4668-A0E9-E34B7235D0BC
// Assembly location: C:\Users\bojac\Downloads\WindowsFormsApp2.exe

using System;
using System.Windows.Forms;

namespace WindowsFormsApp2
{
  internal static class Program
  {
    [STAThread]
    private static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new UrlShortenerForm());
    }
  }
}
