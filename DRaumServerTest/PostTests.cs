using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using DRaumServerApp.Authors;
using DRaumServerApp.Postings;

namespace DRaumServerTest
{
  [TestClass]
  public class PostTests
  {
    [TestMethod]
    public void postVotePercentageTest()
    {
      Posting posting = new Posting(10, "TestPost", 99);
      Assert.AreEqual(50, posting.getUpVotePercentage());  
      posting.voteup(10);
      posting.voteup(10);
      posting.votedown(5);
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
          string res = pmgr.acceptPost(pair.Key, PostingPublishManager.PublishHourType.Normal);
          Assert.IsTrue(res.StartsWith("Veröffentlichung"));
          pmgr.testPublishing(pair.Key, DateTime.Now.AddHours(-24));
          for (int i = 0; i < count * 3;i++)
          {
            if(count > numposts/2)
            {
              pmgr.downvote(pair.Key, 200 + i);
            }
            else
            {
              pmgr.upvote(pair.Key, 200 + i);
            }            
          }
        }
      } while (pair.Key != -1);

      Assert.AreEqual(numposts, count);
      List<long> list = pmgr.getDailyTopPostsFromYesterday();
      bool found10 = false;
      bool found09 = false;
      bool found08 = false;
      foreach (long postingId in list)
      {
        if (postingId == 10)
        {
          found10 = true;
        }
        if (postingId == 9)
        {
          found09 = true;
        }
        if (postingId == 8)
        {
          found08 = true;
        }
      }

      Assert.IsTrue(found10 && found09 && found08);
    }

    [TestMethod]
    public void postDoubleVoteDenyTest()
    {
      PostingManager pmgr = new PostingManager();
      AuthorManager amgr = new AuthorManager();
      Utilities.Runningintestmode = false;
      amgr.setPostMode(10,"user1");
      amgr.setPostMode(20, "user2");
      pmgr.addPosting("testpost", 10);
      KeyValuePair<long, string> kvp = pmgr.getNextPostToCheck();
      long postingId = kvp.Key;
      pmgr.acceptPost(postingId, PostingPublishManager.PublishHourType.Normal);
      Assert.IsFalse(pmgr.isAuthor(postingId,20));
      Assert.IsTrue(amgr.canUserVote(postingId,20,"hans"));
      amgr.vote(postingId,20);
      pmgr.upvote(postingId,5);
      Assert.IsFalse( !pmgr.isAuthor(postingId,20) && amgr.canUserVote(postingId,20,"hans") );
      Assert.IsFalse( !pmgr.isAuthor(postingId,10) && amgr.canUserVote(postingId,10,"hans") );
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
        posting.setChatMessageId(20);
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
            this.processMtTest(ref posting);
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

    private void processMtTest(ref Posting posting)
    {
      posting.voteup(5);
      Thread.Sleep(10);
      Assert.AreEqual(20, posting.getChatMessageId());
      Thread.Sleep(10);
      posting.setPublishTimestamp(DateTime.Now);
      Thread.Sleep(10);
      posting.getPostStatisticText();
      posting.getPostingText();
      Thread.Sleep(10);
      posting.getUpVotePercentage();
      Thread.Sleep(10);
      posting.flag();
      Assert.AreEqual(true, posting.isFlagged());
      Assert.AreEqual(true, posting.isDirty());
    }

    
  }
}
