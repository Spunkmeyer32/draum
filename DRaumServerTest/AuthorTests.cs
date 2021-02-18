using DRaumServerApp;
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
    public void authorDataAndLevelTest()
    {
      Author author = new Author(10, "testuser");
      Assert.AreEqual("testuser", author.getAuthorName());
      Assert.AreEqual(10, author.getAuthorId());
      Assert.AreEqual(1,author.getLevel());
      Assert.AreEqual(PostingPublishManager.publishHourType.HAPPY, author.getPublishType(10));
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      Assert.AreEqual(PostingPublishManager.publishHourType.NORMAL, author.getPublishType(10));
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      author.publishedSuccessfully();
      Assert.AreEqual(4,author.getLevel());
      Assert.AreEqual(PostingPublishManager.publishHourType.PREMIUM, author.getPublishType(3));
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

    }

    [TestMethod]
    public void authorStatisticTest()
    {
      AuthorManager atm = new AuthorManager();
      int median;
      int top;
      atm.getMedianAndTopLevel(out median, out top);
      Assert.AreEqual(0, median);
      Assert.AreEqual(0, top);

      atm.isCoolDownOver(10, "testuser1", Author.InteractionCooldownTimer.DEFAULT);
      atm.getMedianAndTopLevel(out median, out top);
      Assert.AreEqual(1, median);
      Assert.AreEqual(1, top);

      atm.isCoolDownOver(20, "testuser2", Author.InteractionCooldownTimer.DEFAULT);
      atm.isCoolDownOver(30, "testuser3", Author.InteractionCooldownTimer.DEFAULT);
      atm.isCoolDownOver(40, "testuser4", Author.InteractionCooldownTimer.DEFAULT);
      atm.isCoolDownOver(50, "testuser5", Author.InteractionCooldownTimer.DEFAULT);
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
        am.getCoolDownTimer(i * 10 + 1, "user" + i, Author.InteractionCooldownTimer.DEFAULT);
      }
      Assert.ThrowsException<DRaumException>(() => { am.getCoolDownTimer(5000, "toomuch", Author.InteractionCooldownTimer.DEFAULT); });
    }
  }
}
