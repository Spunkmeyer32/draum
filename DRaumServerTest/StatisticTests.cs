using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace DRaumServerTest
{
  [TestClass]
  public class StatisticTests
  {

    [TestMethod]
    public void topPostClassificationTest()
    {
      DRaumStatistics drs = new DRaumStatistics();

      drs.setVotesMedian(200);
      Assert.AreEqual(true, drs.isTopPost(100, 200));
      Assert.AreEqual(false, drs.isTopPost(99, 200));
      Assert.AreEqual(false, drs.isTopPost(100, 199));
      Assert.AreEqual(false, drs.isTopPost(0, 0));
      Assert.AreEqual(true, drs.isTopPost(23476238, 23476238));
      drs.setVotesMedian(500);
      Assert.AreEqual(true, drs.isTopPost(250, 500));
      Assert.AreEqual(false, drs.isTopPost(249, 499));
    }

    [TestMethod]
    public void premiumWriterClassificationTest()
    {
      DRaumStatistics drs = new DRaumStatistics();
      drs.updateWritersLevel(250, 30);
      Assert.AreEqual(140, drs.getPremiumLevelCap());
    }

  }
}
