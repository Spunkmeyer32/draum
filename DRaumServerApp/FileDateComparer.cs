using System.Collections;
using System.IO;

namespace DRaumServerApp
{
  public class FileDateComparer : IComparer
  {
    public int Compare(object x, object y)
    {
      return ((FileInfo)x).LastWriteTime.CompareTo(((FileInfo)y).LastWriteTime);
    }
  }
}