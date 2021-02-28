using DRaumServerApp;
using DRaumServerApp.Authors;
using DRaumServerApp.Postings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRaumServerTest
{
  [TestClass]
  public class AuthorTests
  {

    [TestMethod]
    public void authorFlaggingTest()
    {
      Author author = new Author(20, "test");
      Assert.AreEqual(true, author.canFlag(100));
      author.flag(100);
      Assert.AreEqual(false, author.canFlag(100));
    }

    [TestMethod]
    public void authorVoteGaugeTest()
    {
      Author author = new Author(40, "Testi");
      Assert.AreEqual(10, author.voteUpAndGetCount());// 0
      Assert.AreEqual(10, author.voteUpAndGetCount());// 1
      Assert.AreEqual(9, author.voteUpAndGetCount());// 2
      Assert.AreEqual(9, author.voteUpAndGetCount());// 3
      Assert.AreEqual(8, author.voteUpAndGetCount());// 4
      Assert.AreEqual(8, author.voteUpAndGetCount());// 5
      Assert.AreEqual(7, author.voteUpAndGetCount());// 6
      Assert.AreEqual(7, author.voteUpAndGetCount());// 7
      Assert.AreEqual(6, author.voteUpAndGetCount());// 8
      Assert.AreEqual(6, author.voteUpAndGetCount());// 9
      Assert.AreEqual(5, author.voteUpAndGetCount());// 10
      Assert.AreEqual(5, author.voteUpAndGetCount());// 10
      Assert.AreEqual(5, author.voteUpAndGetCount());// 10
      Assert.AreEqual(10, author.voteDownAndGetCount());// 10
      Assert.AreEqual(10, author.voteDownAndGetCount());// 9
      Assert.AreEqual(10, author.voteDownAndGetCount());// 8
      Assert.AreEqual(10, author.voteDownAndGetCount());// 7
      Assert.AreEqual(10, author.voteDownAndGetCount());// 6
      Assert.AreEqual(10, author.voteDownAndGetCount());// 5
      Assert.AreEqual(10, author.voteDownAndGetCount());// 4
      Assert.AreEqual(10, author.voteDownAndGetCount());// 3
      Assert.AreEqual(10, author.voteDownAndGetCount());// 2
      Assert.AreEqual(10, author.voteDownAndGetCount());// 1
      Assert.AreEqual(10, author.voteDownAndGetCount());// 0
      Assert.AreEqual(9, author.voteDownAndGetCount());// -1
      Assert.AreEqual(8, author.voteDownAndGetCount());// -2
      Assert.AreEqual(7, author.voteDownAndGetCount());// -3
      Assert.AreEqual(6, author.voteDownAndGetCount());// -4
      Assert.AreEqual(5, author.voteDownAndGetCount());// -5
      Assert.AreEqual(4, author.voteDownAndGetCount());// -6
      Assert.AreEqual(3, author.voteDownAndGetCount());// -7
      Assert.AreEqual(2, author.voteDownAndGetCount());// -8
      Assert.AreEqual(1, author.voteDownAndGetCount());// -9
      Assert.AreEqual(1, author.voteDownAndGetCount());// -10
      Assert.AreEqual(1, author.voteDownAndGetCount());// -10
      Assert.AreEqual(7, author.getLevel());

    }

    [TestMethod]
    public void authorModesTest()
    {
      Author author = new Author(10, "Testy");
      Assert.IsFalse(author.isInFeedbackMode());
      Assert.IsFalse(author.isInPostMode());
      author.setFeedbackMode();
      Assert.IsTrue(author.isInFeedbackMode());
      Assert.IsFalse(author.isInPostMode());
      author.unsetModes();
      Assert.IsFalse(author.isInFeedbackMode());
      Assert.IsFalse(author.isInPostMode());
      author.setPostMode();
      Assert.IsTrue(author.isInPostMode());
      Assert.IsFalse(author.isInFeedbackMode());
      author.unsetModes();
      Assert.IsFalse(author.isInFeedbackMode());
      Assert.IsFalse(author.isInPostMode());
    }


    [TestMethod]
    public void authorDataAndLevelTest()
    {
      Author author = new Author(10, "testuser");
      Assert.AreEqual("testuser", author.getAuthorName());
      Assert.AreEqual(10, author.getAuthorId());
      Assert.AreEqual(1,author.getLevel());
      Assert.AreEqual(PostingPublishManager.PublishHourType.Happy, author.getPublishType(10));
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      Assert.AreEqual(PostingPublishManager.PublishHourType.Normal, author.getPublishType(10));
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      Assert.AreEqual(4,author.getLevel());
      Assert.AreEqual(PostingPublishManager.PublishHourType.Premium, author.getPublishType(3));
      for (long i = 0; i < 20000; i++)
      {
        author.publishedSuccessfully();
      }

      Assert.AreEqual(268,author.getLevel());
      for (long i = 0; i < 50000; i++)
      {
        author.publishedSuccessfully();
      }

      Assert.AreEqual(500,author.getLevel());
      for (long i = 0; i < 10000; i++)
      {
        author.publishedSuccessfully();
      }

      Assert.AreEqual(500,author.getLevel());

      author.updateCredibility(70,30);
      Assert.IsTrue(author.getShortAuthorInfo().EndsWith("70 Prozent Zustimmung"));

    }

    [TestMethod]
    public void authorManagerTest()
    {
      AuthorManager atm = new AuthorManager();
      // Legt on-the-fly einen nutzer neu an:
      atm.isCoolDownOver(20, "testuser2", Author.InteractionCooldownTimer.Default);

      Assert.IsTrue(atm.getPublishType(10,100) == PostingPublishManager.PublishHourType.None);
      Assert.IsTrue(atm.getPublishType(20,100) == PostingPublishManager.PublishHourType.Happy);

      Assert.IsFalse(atm.isPostMode(10,"buh")); // legt als side-effect den nutzer 10 an
      atm.setPostMode(10,"Test User 2");
      atm.setPostMode(20,"boho");
      Assert.IsTrue(atm.isPostMode(10,"Test10"));
      Assert.IsTrue(atm.isPostMode(20,"Test20"));

      Assert.AreEqual(2, atm.getAuthorCount());

    }

    [TestMethod]
    public void authorStatisticTest()
    {
      AuthorManager atm = new AuthorManager();
      atm.getMedianAndTopLevel(out var median, out var top);
      Assert.AreEqual(0, median);
      Assert.AreEqual(0, top);

      atm.isCoolDownOver(10, "testuser1", Author.InteractionCooldownTimer.Default);
      atm.getMedianAndTopLevel(out median, out top);
      Assert.AreEqual(1, median);
      Assert.AreEqual(1, top);

      atm.isCoolDownOver(20, "testuser2", Author.InteractionCooldownTimer.Default);
      atm.isCoolDownOver(30, "testuser3", Author.InteractionCooldownTimer.Default);
      atm.isCoolDownOver(40, "testuser4", Author.InteractionCooldownTimer.Default);
      atm.isCoolDownOver(50, "testuser5", Author.InteractionCooldownTimer.Default);
      atm.getMedianAndTopLevel(out median, out top);
      Assert.AreEqual(1, median);
      Assert.AreEqual(1, top);

      atm.publishedSuccessfully(10);
      atm.publishedSuccessfully(10);
      atm.publishedSuccessfully(20);
      atm.publishedSuccessfully(20);
      atm.publishedSuccessfully(20);
      atm.publishedSuccessfully(30);
      atm.publishedSuccessfully(40);
      atm.publishedSuccessfully(40);
      atm.publishedSuccessfully(40);
      atm.publishedSuccessfully(50);
      atm.publishedSuccessfully(50);
      atm.publishedSuccessfully(50);
      atm.publishedSuccessfully(50);
      atm.publishedSuccessfully(50);
      atm.getMedianAndTopLevel(out median, out top);
      Assert.AreEqual(2, median);
      Assert.AreEqual(3, top);

      DRaumStatistics drs = new DRaumStatistics();
      drs.updateWritersLevel(top, median);
      Assert.AreEqual(2, drs.getPremiumLevelCap());

    }

    [TestMethod]
    public void maxManagedAuthorsTest()
    {
      AuthorManager am = new AuthorManager();
      AuthorManager.Maxmanagedusers = 20;
      for(int i=0;i<AuthorManager.Maxmanagedusers;i++)
      {
        am.getCoolDownTimer(i * 10 + 1, "user" + i, Author.InteractionCooldownTimer.Default);
      }

      Assert.ThrowsException<DRaumException>(() => { am.getCoolDownTimer(5000, "toomuch", Author.InteractionCooldownTimer.Default); });
    }
  }
}
