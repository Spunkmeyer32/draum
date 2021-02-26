using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;


namespace DRaumServerApp
{
  public class DRaumException : Exception
  {
    public DRaumException(string message)
        : base(message)
    {
    }


  }
}
