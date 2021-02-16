using System;
using System.IO;
using DRaumServerApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DRaumServerTest
{
  [TestClass]
  public class MassPostingTest
  {

    [TestMethod]
    public void checkMassPostings()
    {
      AuthorManager.Maxmanagedusers = int.MaxValue;
      Utilities.RUNNINGINTESTMODE = true;
      AuthorManager authors = new AuthorManager();
      PostingManager postings = new PostingManager();

      NLog.LogManager.DisableLogging();

      for (int i = 0; i < 5000; i++)
      {
        authors.getCoolDownTimer(10000+i, "autor_" + i, Author.InteractionCooldownTimer.DEFAULT);
      }

      int authorid = 10000;
      for (int monat = 1; monat <= 6; monat++)
      {
        for (int tag = 1; tag <= 30; tag++)
        {
          for (int postcount = 0; postcount < 200; postcount++)
          {
            postings.addPosting("Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt " +
                                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo " +
                                "dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit " +
                                "amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor " +
                                "invidunt ut labor"+postcount+":"+tag+":"+monat, authorid);
            authorid++;
            if (authorid > 14999)
            {
              authorid = 10000;
            }
          }
        }
      }
      DateTime before = DateTime.Now;
      FileStream backupfile = System.IO.File.Create("testposts.json");
      StreamWriter sr = new StreamWriter(backupfile);
      sr.Write(JsonConvert.SerializeObject(postings, Formatting.Indented));
      sr.Close();
      TimeSpan durationToStore = DateTime.Now - before;

      Console.Out.WriteLine("Speichern dauerte " + durationToStore.TotalSeconds + " Sekunden.");

      before = DateTime.Now;
      FileStream inputFilestream = System.IO.File.OpenRead("testposts.json");
      StreamReader sreader = new StreamReader(inputFilestream);
      string jsonstring = sreader.ReadToEnd();
      sreader.Close();
      postings = JsonConvert.DeserializeObject<PostingManager>(jsonstring);
      durationToStore = DateTime.Now - before;

      Console.Out.WriteLine("Laden dauerte " + durationToStore.TotalSeconds + " Sekunden.");

      Assert.IsTrue(true);



    }



  }
}