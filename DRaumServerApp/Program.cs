using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using DRaumServerApp.Authors;

namespace DRaumServerApp
{
  internal static class Program
  {

    private static readonly System.Timers.Timer timer = new System.Timers.Timer(1000) { AutoReset = true };
    private static readonly AutoResetEvent autoResetEvent = new AutoResetEvent(false);
    private static bool _stopApp;
    private static bool _sigintRec;

    private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      autoResetEvent.Set();
    }

    /// <summary>
    /// .NET Core 3.1 benutzt die Language-Specification 8, welche async Task Main erlaubt.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedParameter.Local
    static async Task Main(string[] args)
    {
      var logger = NLog.LogManager.GetCurrentClassLogger();
      logger.Info("D-Raum-Server startet");
      try
      {
        Author.checkForTestingMode();
        DRaumManager.checkForTestingMode();
        DRaumManager dRaumManager = new DRaumManager();
        dRaumManager.initData();
        await dRaumManager.start();

        logger.Info("D-Raum-Server läuft");
        timer.Elapsed += Timer_Elapsed;
        timer.Start();
        Console.CancelKeyPress += async delegate (object sender, ConsoleCancelEventArgs e)
        {
          if (_stopApp)
          {
            logger.Error("Beenden(sigint) schon angefordert, das geht nicht zweimal");
            return;
          }
          _sigintRec = true;
          e.Cancel = true;
          logger.Info("D-Raum-Server endet (SIGINT)");
          await dRaumManager.shutDown();
          _stopApp = true;
          logger.Info("D-Raum-Server ist beendet");
        };
        AppDomain.CurrentDomain.ProcessExit += async (sender, eargs) =>
        {
          if (!_sigintRec)
          {
            if (_stopApp)
            {
              logger.Error("Beenden(sigterm) schon angefordert, das geht nicht zweimal");
              return;
            }
            logger.Info("D-Raum-Server endet (SIGTERM)");
            await dRaumManager.shutDown();
            _stopApp = true;
            logger.Info("D-Raum-Server ist beendet");
          }
          else
          {
            logger.Info("SIGTERM ignoriert, da SIGINT bekommen");
          }
        };
        while (true)
        {
          if(!autoResetEvent.WaitOne(3000))
          {
            logger.Warn("Der Warte-Timer der Haupt-Schleife wurde nicht benachrichtigt, Flaschenhals??");
          }
          if(_stopApp)
          {
            break;
          }
        }
      }
      catch(Exception ex)
      {
        logger.Error(ex, "Unbehandelter Fehler im Programm");
      }
      finally
      {
        NLog.LogManager.Shutdown();
      }      
    }
  }
}
