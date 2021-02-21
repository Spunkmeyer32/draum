using System.Collections;

namespace DRaumServerApp.Postings
{
  internal class PostingVoteComparer : IComparer
  {
    public int Compare(object x, object y)
    {
      if (x == null && y == null)
      {
        return 0;
      }
      if (x == null)
      {
        return -1;
      }
      if (y == null)
      {
        return 1;
      }
      if (((Posting)y).getVoteCount() < ((Posting)x).getVoteCount())
      {
        return -1;
      }
      return ((Posting)y).getVoteCount() > ((Posting)x).getVoteCount() ? 1 : 0;
    }
  }
}