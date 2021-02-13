using System.Threading;

namespace DRaumServerApp
{
  /// <summary>
  /// Um zyklische Tasks synchron anzuhalten ohne sie zu beenden kann diese Klasse verwendet werden.
  /// </summary>
  internal static class SyncManager
  {
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private static int _runningTasks = 0;
    private static readonly object tasksmutex = new object();
    private static volatile bool _shouldWait = false;
    private static readonly ManualResetEvent resetEvent = new ManualResetEvent(false);
    private static ManualResetEvent _externalEvent = null;

    /// <summary>
    /// Bevor der Task seine Arbeit verrichtet, sollte er diese Funktion aufrufen, welche eventuell zu synchronisationszwecken blockt
    /// </summary>
    /// <param name="cancelToken">Falls andere Tasks irgendwo hängen bleiben, kann der Aufruf abgebrochen werden</param>
    internal static void tryRun(CancellationToken cancelToken)
    {
      if (!_shouldWait)
      {
        return;
      }
      lock (tasksmutex)
      {
        _runningTasks -= 1;
        if (_runningTasks == 0)
        {
          logger.Info("Alle Tasks haben gehalten");
          if (_externalEvent != null)
          {
            _externalEvent.Set();
          }
          else
          {
            unhalt();
          }
        }
      }
      // Auf alle anderen Tasks warten
      while (!cancelToken.IsCancellationRequested)
      {
        if (resetEvent.WaitOne(300))
        {
          break;
        }
      }
      // Weiter
      lock (tasksmutex)
      {
        _runningTasks += 1;
      }
    }

    /// <summary>
    /// Alle Tasks werden angehalten und der Aufrufer wird über das Reset-Event-Signal informiert
    /// </summary>
    /// <param name="extEvnt"></param>
    internal static void halt(ManualResetEvent extEvnt)
    {
      logger.Info(_runningTasks + " Tasks werden angehalten");
      _externalEvent = extEvnt;
      resetEvent.Reset();
      _shouldWait = true;
    }

    internal static bool isHaltingRequested()
    {
      return _shouldWait;
    }

    /// <summary>
    /// Alle Tasks weiterführen
    /// </summary>
    internal static void unhalt()
    {
      logger.Info(_runningTasks + " Tasks werden fortgeführt");
      _shouldWait = false;
      _externalEvent = null;
      resetEvent.Set();
    }

    /// <summary>
    /// Tasks müssen sich selbst registrieren, um gezählt zu werden
    /// </summary>
    internal static void register()
    {
      lock (tasksmutex)
      {
        _runningTasks += 1;
        logger.Info("Nun " + _runningTasks + " Tasks registriert");
      }
    }

    /// <summary>
    /// Wenn ein Task beendet wird, muss er sich abmelden
    /// </summary>
    internal static void unregister()
    {
      lock (tasksmutex)
      {
        _runningTasks -= 1;
        logger.Info("Nun noch " + _runningTasks + " Tasks registriert");
      }
    }

  }
}