// Decompiled with JetBrains decompiler
// Type: WindowsFormsApp2.Properties.Resources
// Assembly: WindowsFormsApp2, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6013BA42-02F4-4668-A0E9-E34B7235D0BC
// Assembly location: C:\Users\bojac\Downloads\WindowsFormsApp2.exe

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace WindowsFormsApp2.Properties
{
  [GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
  [DebuggerNonUserCode]
  [CompilerGenerated]
  internal class Resources
  {
    private static ResourceManager resourceMan;
    private static CultureInfo resourceCulture;

    internal Resources()
    {
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static ResourceManager ResourceManager
    {
      get
      {
        if (resourceMan == null)
          resourceMan = new ResourceManager("WindowsFormsApp2.Properties.Resources", typeof (Resources).Assembly);
        return resourceMan;
      }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static CultureInfo Culture
    {
      get => resourceCulture;
      set => resourceCulture = value;
    }
  }
}
