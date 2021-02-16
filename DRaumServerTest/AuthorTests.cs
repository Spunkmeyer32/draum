using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRaumServerTest
{
  [TestClass]
  public class AuthorTests
  {
    [TestMethod]
    public void authorStatisticTest()
    {
      AuthorManager atm = new AuthorManager();
      int median = 0;
      int top = 0;
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
      Assert.AreEqual(5, median);
      Assert.AreEqual(8, top);

      DRaumStatistics drs = new DRaumStatistics();
      drs.updateWritersLevel(top, median);
      Assert.AreEqual(6, drs.getPremiumLevelCap());

    }

    [TestMethod]
    public void maxManagedAuthorsTest()
    {
      AuthorManager am = new AuthorManager();
      for(int i=0;i<AuthorManager.Maxmanagedusers;i++)
      {
        am.getCoolDownTimer(i * 10 + 1, "user" + i, Author.InteractionCooldownTimer.DEFAULT);
      }
      Assert.ThrowsException<DRaumException>(() => { am.getCoolDownTimer(5000, "toomuch", Author.InteractionCooldownTimer.DEFAULT); });
      
    }
  }
}
