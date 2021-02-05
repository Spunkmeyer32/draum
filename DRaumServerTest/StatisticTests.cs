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
      Assert.AreEqual(true, drs.isTopPost(50, 200));
      Assert.AreEqual(false, drs.isTopPost(49, 200));
      Assert.AreEqual(false, drs.isTopPost(50, 199));
      Assert.AreEqual(false, drs.isTopPost(0, 0));
      Assert.AreEqual(true, drs.isTopPost(100, 23476238));
      drs.setVotesMedian(500);
      Assert.AreEqual(true, drs.isTopPost(50, 500));
      Assert.AreEqual(false, drs.isTopPost(50, 499));
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
