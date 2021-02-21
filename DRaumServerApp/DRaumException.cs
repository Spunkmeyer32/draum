using System;


namespace DRaumServerApp
{
  internal class DRaumException : Exception
  {
    public DRaumException(string message)
        : base(message)
    {
    }

  }
}
