using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DRaumServerApp
{
  class DRaumException : Exception
  {
    public DRaumException()
    {
    }

    public DRaumException(string message)
        : base(message)
    {
    }

    public DRaumException(string message, Exception inner)
        : base(message, inner)
    {
    }
  }
}
