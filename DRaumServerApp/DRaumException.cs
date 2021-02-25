using System;


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
