// Decompiled with JetBrains decompiler
// Type: UrlShortener.Failures.NetworkFailure
// Assembly: UrlShortener, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6013BA42-02F4-4668-A0E9-E34B7235D0BC
// Assembly location: C:\Users\bojac\Downloads\UrlShortener.exe

using System;

namespace UrlShortener.Failures
{
  public class NetworkFailure : Exception
  {
    protected NetworkFailure(string message)
      : base(message)
    {
    }
  }
}
