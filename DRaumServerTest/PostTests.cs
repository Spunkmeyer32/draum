using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace DRaumServerTest
{
  [TestClass]
  public class PostTests
  {
    [TestMethod]
    public void postVotePercentageTest()
    {
      DRaumServerApp.Posting posting = new DRaumServerApp.Posting(10, "TestPost", 99);
      posting.voteup(1, 10);
      posting.voteup(2, 10);
      posting.votedown(3, 5);
      // 20 / 25 = 0,8 * 100 = 80 ceil = 80
      Assert.AreEqual(80, posting.getUpVotePercentage());      
    }

    [TestMethod]
    public void postDoubleVoteDenyTest()
    {
      DRaumServerApp.Posting posting = new DRaumServerApp.Posting(10, "TestPost", 99);
      posting.voteup(10, 10);
      Assert.IsFalse(posting.canUserVote(10));
      posting.votedown(20, 10);
      Assert.IsFalse(posting.canUserVote(20));
    }

    [TestMethod]
    public void postMultithreadTest()
    {
      try
      {
        int numThreads = 250;
        ManualResetEvent resetEvent = new ManualResetEvent(false);
        ManualResetEvent startEvent = new ManualResetEvent(false);
        int toProcess = numThreads;
        int waitForAll = 0;
        Posting posting = new Posting(33, "TestPost", 192923);
        posting.setChatMessageID(20);
        for (int i = 0; i < numThreads; i++)
        {
          new Thread(delegate ()
          {
            if (Interlocked.Increment(ref waitForAll) == numThreads)
            {
              startEvent.Set();
            }
            // Threads laufen bis hier hin, warten dann auf das Signal des letzten Threads
            startEvent.WaitOne();
            processMTTest(i + 10, ref posting);
            if (Interlocked.Decrement(ref toProcess) == 0)
            { 
              resetEvent.Set();
            } 
          }).Start();
        }
        resetEvent.WaitOne();
        Assert.IsTrue(true);
      }
      catch(Exception e)
      {
        Assert.Fail(e.Message);
      }      
    }

    private void processMTTest(int userid, ref Posting posting)
    {
      posting.voteup(userid, 5);
      Assert.AreEqual(false, posting.canUserVote(userid));
      Thread.Sleep(10);
      Assert.AreEqual(20, posting.getChatMessageID());
      Thread.Sleep(10);
      posting.setPublishTimestamp(DateTime.Now);
      Thread.Sleep(10);
      posting.getPostStatisticText();
      posting.getPostingText();
      Thread.Sleep(10);
      posting.getUpVotePercentage();
      Thread.Sleep(10);
      posting.flag(userid);
      Assert.AreEqual(true, posting.isFlagged());
      Assert.AreEqual(true, posting.isDirty());
    }

    
  }
}
