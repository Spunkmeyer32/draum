using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
    public void topPostingFilterTest()
    {
      PostingManager pmgr = new PostingManager();
      DRaumStatistics drs = new DRaumStatistics();
      const int numposts = 20;
      for(int i=0;i< numposts; i++)
      {
        // ein paar posts anlegen
        pmgr.addPosting("Bla bla bla", 10+i*10);
      }

      int count = 0;
      KeyValuePair<long, string> pair;
      do
      {
        pair = pmgr.getNextPostToCheck();
        if(pair.Key!=-1)
        {
          count++;
          String res = pmgr.acceptPost(pair.Key, PostingPublishManager.publishHourType.NORMAL);
          Assert.IsTrue(res.StartsWith("Veröffentlichung"));
          pmgr.testPublishing(pair.Key, DateTime.Now.AddHours(-24));
          for (int i = 0; i < count * 3;i++)
          {
            if(count > numposts/2)
            {
              pmgr.downvote(pair.Key, 200 + i, 10);
            }
            else
            {
              pmgr.upvote(pair.Key, 200 + i, 10);
            }            
          }
        }
      } while (pair.Key != -1);
      Assert.AreEqual(numposts, count);
      List<Posting> list = pmgr.getDailyTopPostsFromYesterday();
      bool found10 = false;
      bool found09 = false;
      bool found08 = false;
      foreach (Posting posting in list)
      {
        if (posting.getPostID() == 10)
        {
          found10 = true;
        }
        if (posting.getPostID() == 9)
        {
          found09 = true;
        }
        if (posting.getPostID() == 8)
        {
          found08 = true;
        }
      }
      Assert.IsTrue(found10 && found09 && found08);
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
