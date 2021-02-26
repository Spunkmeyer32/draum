using System;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using DRaumServerApp.CyclicTasks;
using DRaumServerApp.TelegramUtilities;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/*
Willkommen in der Kommentarspalte des Internets auf Telegram ;-)

Beta-Phase: Es kann noch zu Software-Fehlern kommen, bitte Feedback über den Bot @d_raum_input_bot !

Meine Idee:
Auf Twitter und anderen Plattformen kann man gut Stimmungen einzelner Echokammern/Filterblasen verfolgen.
Das verzerrt aufgrund der eigenen Filterblase und durch Blockierungen jedoch das Gesamtbild.

Hier in diesem D-Raum auf Telegram sollen Meinungen aus verschiedenen 
Echokammern zusammentreffen und bewertet werden um ein übergreifendes Stimmungsbild zu zeigen. 
(Das D steht für vieles, suchen Sie sich was aus)

Damit dies gelingt, sind ein paar Regeln einzuhalten:

Die Texte hier sollen Kommentare sein, keine Kopien von Nachrichten im Netz (Urheberrecht und Sinn eines Meinungsforums).
Sie sollen nicht gegen Gesetze verstoßen, sonst wird der Kanal zu gemacht.
Keine offene Hetze oder persönliche Beleidigungen. Damit das auch eingehalten wird,
moderiere ich die Beiträge (nur freischalten oder mit Änderungen, die der Autor oder die Autorin bestätigen muss).

Damit keine Fan-Basis und Echokammern entstehen werden die Beiträge und die 
Bewertungen anonymisiert. Gegen Spam arbeitet im Hintergrund ein Bot-Programm, welches stetig 
verbessert wird. Zur Zeit ist eine Beitragsrate von 20 Minuten eingestellt. Wer etwas schreibt, bekommt den 
nächsten freien Veröffentlichungs-Slot zugeteilt und angezeigt.

Im Hauptkanal https://t.me/d_raum werden tagsüber die Beiträge veröffentlicht.
Jeden Tag werden die drei meistbewerteten Beiträge in dem Kanal https://t.me/d_raum_daily verlinkt. 
Einmal pro Woche werden die Top-5 Beiträge in dem Kanal https://t.me/d_raum_weekly verlinkt. 

Beiträge werden gelöscht, wenn sie entweder zu alt werden, viele negative Stimmen 
erhalten oder gegen Gesetze verstoßen. Abstimmen kann jeder mit 
einem Telegram-Account durch klick auf die Knöpfe unter den Beiträgen.

Selbst veröffentlichen, Feedback geben und weiteres kann man mit dem Eingabe-Bot @d_raum_input_bot
*/

namespace DRaumServerApp
{
  
  /// <summary>
  /// Hauptklasse für den D-Raum Telegram-Bot-Server
  /// </summary>
  internal class DRaumManager
  {
    #region dynamicmembers
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    // D-Raum Daten
    private Authors.AuthorManager authors;
    private Postings.PostingManager posts;
    private DRaumStatistics statistics;
    private FeedbackManager feedbackManager;
    private PostingTextBuilder textBuilder;

    private CyclicTasks.FeedbackSendingTask feedbackSendingTask;
    private CyclicTasks.PublishingTask publishingTask;
    private CyclicTasks.VoteAndFlagTask voteAndFlagTask;
    private CyclicTasks.StatisticCollectionTask statisticCollectionTask;

    // Telegram Bots
    private TelegramBotClient telegramInputBot;
    private TelegramBotClient telegramPublishBot;
    private TelegramBotClient telegramFeedbackBot;
    private TelegramBotClient telegramModerateBot;
    private TelegramBotClient telegramAdminBot;

    private Bots.InputBot inputBot;
    private Bots.AdminBot adminBot;

    // Chats (mit dem Admin, Moderator, Feedback und zur Veröffentlichung in Kanälen)
    private readonly long feedbackChatId;
    private readonly long moderateChatId;
    private readonly long adminChatId;
    private readonly long draumChatId;
    private readonly long draumDailyChatId;
    private readonly long draumWeeklyChatId;

    
    private Task backupTask;
    private Task postAndFeedbackCheckingTask;


    private string startupinfo = "Keine Info";

    #endregion

    // Statische Optionen
    private const string BackupFolder = "backups";
    private const string FilePrefix = "_draum_";
    internal const string Roomname = "d_raum";
    private const string Writecommand = "schreiben";
    private const string Feedbackcommand = "nachricht";

    private static readonly UpdateType[] receivefilterCallbackAndMessage = {UpdateType.CallbackQuery, UpdateType.Message };
    private static readonly UpdateType[] receivefilterCallbackOnly = {UpdateType.CallbackQuery, UpdateType.Message };

    private readonly CancellationTokenSource cancelTasksSource = new CancellationTokenSource();

    // Tasks und Intervalle für das regelmäßige Abarbeiten von Aufgaben
    
    private static readonly int intervalBackUpDataMinutes = 60;
    private static readonly int intervalpostcheckMilliseconds = 500;


    // Vorgefertigte Texte
    internal static readonly string PostIntro = "Schreib-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Posting interpretiert. " +
                                                "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1500\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                "ähnliches\r\n▫️Der Text muss sich im Rahmen der Gesetze bewegen\r\n▫️Keine Urheberrechtsverletzungen\r\n\r\nDer Text wird dann maschinell und ggf. durch " +
                                                "Menschen gegengelesen und wird bei eventuellen Anpassungen in diesem Chat zur Bestätigung durch Sie nochmal abgebildet. " +
                                                "Das Posting wird anonym veröffentlicht. Ihre User-ID wird intern gespeichert.";

    internal static readonly string FeedbackIntro = "Feedback-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Feedback für Moderatoren und Kanalbetreiber weitergeleitet. " +
                                                    "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1500\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                    "ähnliches.\r\n\r\nIhre User-ID wird für eine eventuelle Rückmeldung gespeichert.";

    internal static readonly string NoModeChosen = "Willkommen beim D-Raum-Input-Bot 🤖.\r\n\r\nEs ist zur Zeit kein Modus gewählt! Mit /" + Writecommand + " schaltet man in den Beitrag-Schreiben-Modus. Mit /" + Feedbackcommand + " kann man in den Feedback-Modus gelangen und den Moderatoren und Kanalbetreibern eine Nachricht "+
                                                   "hinterlassen (Feedback/Wünsche/Kritik).";

    internal static readonly string ReplyPost = "Danke für den Beitrag ✍️.\r\n\r\nEr wird geprüft und vor der Freigabe nochmal in diesem Chat an Sie verschickt zum gegenlesen. Dies kann dauern, bitte Geduld.";

    internal static readonly string ReplyFeedback = "Danke für das Feedback 👍.\r\n\r\nEs wird nun von Moderatoren und Kanalbetreiber gelesen. Sie erhalten eventuell hier in diesem Chat eine Rückmeldung.";
    
    internal DRaumManager()
    {
      string testmode = ConfigurationManager.AppSettings["runInTestMode"];
      if (testmode.Equals("true"))
      {
        Utilities.Runningintestmode = true;
      }
      this.feedbackChatId = long.Parse(ConfigurationManager.AppSettings["feedbackChatID"]);
      this.moderateChatId = long.Parse(ConfigurationManager.AppSettings["moderateChatID"]);
      this.adminChatId = long.Parse(ConfigurationManager.AppSettings["adminChatID"]);
      this.draumChatId = long.Parse(ConfigurationManager.AppSettings["mainRoomID"]);
      this.draumDailyChatId = long.Parse(ConfigurationManager.AppSettings["dailyRoomID"]);
      this.draumWeeklyChatId = long.Parse(ConfigurationManager.AppSettings["weeklyRoomID"]);
    }

    internal void initData()
    {
      logger.Info("Lade Autor-Manager");
      this.authors = new Authors.AuthorManager();
      logger.Info("Lade Posting-Manager");
      this.posts = new Postings.PostingManager();
      logger.Info("Lade Statistik-Manager");
      this.statistics = new DRaumStatistics();
      logger.Info("Lade Feedback-Manager");
      this.feedbackManager = new FeedbackManager();

      if (!this.loadDataFromFiles())
      {
        this.startupinfo = "!!! Server ist ohne Daten gestartet !!!";
        logger.Info("Lade Autor-Manager neu");
        this.authors = new Authors.AuthorManager();
        logger.Info("Lade Posting-Manager neu");
        this.posts = new Postings.PostingManager();
        logger.Info("Lade Statistik-Manager neu");
        this.statistics = new DRaumStatistics();
        logger.Info("Lade Feedback-Manager neu");
        this.feedbackManager = new FeedbackManager();
      }
      else
      {
        this.startupinfo = "Server ist gestartet";
      }

      this.textBuilder = new PostingTextBuilder(this.posts, this.authors);

      this.startupinfo += "\r\nMaximale Autorenzahl:" + Authors.AuthorManager.Maxmanagedusers;
    }

    internal async void start()
    {
      this.telegramInputBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramInputToken"]);
      this.telegramPublishBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramPublishToken"]);
      this.telegramFeedbackBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramFeedbackToken"]);
      this.telegramModerateBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramModerateToken"]);
      this.telegramAdminBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramAdminToken"]);

      Task<Update[]> taskInput = this.telegramInputBot.GetUpdatesAsync();
      Task<Update[]> taskModerate = this.telegramModerateBot.GetUpdatesAsync();
      Task<Update[]> taskPublish = this.telegramPublishBot.GetUpdatesAsync();
      Task<Update[]> taskFeedback = this.telegramFeedbackBot.GetUpdatesAsync();

      this.inputBot = new Bots.InputBot(this.authors, this.statistics, this.telegramInputBot, this.posts, this.feedbackManager);
      this.adminBot = new Bots.AdminBot(this.telegramAdminBot);

      await this.adminBot.sendMessage(this.adminChatId,this.startupinfo +"\r\n" + this.statistics.getHardwareInfo());

      logger.Info("Setze das Offset bei den Nachrichten, um nicht erhaltene Updates zu löschen");
      Update[] updates = taskInput.Result;
      if (updates.Length > 0)
      {
        this.telegramInputBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskModerate.Result;
      if (updates.Length > 0)
      {
        this.telegramModerateBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskPublish.Result;
      if (updates.Length > 0)
      {
        this.telegramPublishBot.MessageOffset = updates[^1].Id + 1;
      }
      updates = taskFeedback.Result;
      if (updates.Length > 0)
      {
        this.telegramFeedbackBot.MessageOffset = updates[^1].Id + 1;
      }

      logger.Info("Beginne das Lauschen auf eingehende Nachrichten...");
      this.telegramInputBot.OnMessage += this.onInputBotMessage;
      this.telegramInputBot.OnCallbackQuery += this.onInputBotCallback;
      this.telegramInputBot.OnReceiveError += this.onReceiveError;
      this.telegramInputBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramInputBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramModerateBot.OnCallbackQuery += this.onModerateCallback;
      this.telegramModerateBot.OnMessage += this.onModerateMessage;
      this.telegramModerateBot.OnReceiveError += this.onReceiveError;
      this.telegramModerateBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramModerateBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramPublishBot.OnCallbackQuery += this.onPublishCallback;
      this.telegramPublishBot.OnReceiveError += this.onReceiveError;
      this.telegramPublishBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramPublishBot.StartReceiving(receivefilterCallbackOnly);

      this.telegramFeedbackBot.OnCallbackQuery += this.onFeedbackCallback;
      this.telegramFeedbackBot.OnMessage += this.onFeedbackMessage;
      this.telegramFeedbackBot.OnReceiveError += this.onReceiveError;
      this.telegramFeedbackBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramFeedbackBot.StartReceiving(receivefilterCallbackAndMessage);

      this.telegramAdminBot.OnCallbackQuery += this.onAdminCallback;
      this.telegramAdminBot.OnReceiveError += this.onReceiveError;
      this.telegramAdminBot.OnReceiveGeneralError += this.onReceiveGeneralError;
      this.telegramAdminBot.StartReceiving(receivefilterCallbackOnly);

      logger.Info("Starte periodische Aufgaben");
      this.feedbackSendingTask = new CyclicTasks.FeedbackSendingTask(this.feedbackManager, this.telegramFeedbackBot, this.feedbackChatId);
      this.publishingTask = new CyclicTasks.PublishingTask(this.telegramPublishBot, this.draumChatId, this.posts,this.draumDailyChatId,this.draumWeeklyChatId, this.textBuilder);
      this.voteAndFlagTask = new CyclicTasks.VoteAndFlagTask(this.posts, this.draumChatId, this.textBuilder,this.telegramPublishBot,this.statistics, this.adminChatId, this.adminBot);
      this.statisticCollectionTask = new StatisticCollectionTask(this.authors,this.statistics,this.posts,this.adminBot,this.adminChatId);
      
      this.backupTask = this.periodicBackupTask(new TimeSpan(0, intervalBackUpDataMinutes, 0), this.cancelTasksSource.Token);
      this.postAndFeedbackCheckingTask = this.periodicInputCheckTask(new TimeSpan(0, 0, 0, 0, intervalpostcheckMilliseconds), this.cancelTasksSource.Token);
      
    }


    // ==  Running Tasks
  


    private async Task periodicInputCheckTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Input-Check-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }
        this.inputCheckTask();
      }

      logger.Info("Input-Check-Task ist beendet");
    }

    private async Task periodicBackupTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Backup-Task ist gestartet");
      while (true)
      {
        try
        {
          await Task.Delay(interval, cancellationToken);
        }
        catch(OperationCanceledException)
        {
          break;
        }
        if(cancellationToken.IsCancellationRequested)
        {
          break;
        }
        await this.backUpTask();
      }

      logger.Info("Backup-Task ist beendet");
    }


    // ==  Task Methods


    private void inputCheckTask()
    {
      // Eingehende, zu moderierende Posts bearbeiten
      if (this.posts.getAndResetPostsCheckChangeFlag())
      {
        int messageId = this.feedbackManager.getModerateMessageId();
        int postsToCheck = this.posts.howManyPostsToCheck();
        string message = "Es gibt " + postsToCheck + " Posts zu moderieren.";
        if (messageId == -1)
        {
          // Gibt noch keine Moderator-Message, neu Anlegen
          try
          {
            Message msg = this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: message,
              replyMarkup: TelegramUtilities.Keyboards.getGetNextPostToModerateKeyboard()).Result;
            this.feedbackManager.setModerateMessageId(msg.MessageId);
          }
          catch(Exception e)
          {
            logger.Error(e, "Fehler beim Anlegen der Moderations-Nachricht");
          }
        }
        else
        {
          // Update der Message
          try
          {
             _ = this.telegramModerateBot.EditMessageTextAsync (
              chatId: this.moderateChatId,
              messageId: messageId,
              text: message,
              replyMarkup: TelegramUtilities.Keyboards.getGetNextPostToModerateKeyboard()).Result;
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Aktualisieren der Moderations-Nachricht");
          }
        }
      }
    }
    
    
    private async Task backUpTask()
    {
      try
      {
        logger.Info("Anhalten für Backup");
        this.telegramInputBot.StopReceiving();
        this.telegramModerateBot.StopReceiving();
        this.telegramPublishBot.StopReceiving();
        this.telegramFeedbackBot.StopReceiving();
        ManualResetEvent mre = new ManualResetEvent(false);
        SyncManager.halt(mre);
        if (!mre.WaitOne(TimeSpan.FromMinutes(3)))
        {
          logger.Error("Die Tasks sind nicht alle angehalten!");
        }
        await this.backupData();
        this.telegramInputBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramModerateBot.StartReceiving(receivefilterCallbackAndMessage);
        this.telegramPublishBot.StartReceiving(receivefilterCallbackOnly);
        this.telegramFeedbackBot.StartReceiving(receivefilterCallbackAndMessage);
        SyncManager.unhalt();
        logger.Info("Backup erledigt, weitermachen");
        this.statistics.setLastBackup(DateTime.Now);
        /// TODO Alte Backups löschen
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler im Backup-Task");
      }
    }

    

    // == Persistency
    private bool loadDataFromFiles()
    {
      // Suche im Backup-Ordner nach dem neuesten Satz backupdateien
      string dateprefix = "";
      DirectoryInfo di = new DirectoryInfo(BackupFolder);
      if(di.Exists)
      {
        FileInfo[] filelist = di.GetFiles();
        Array.Sort(filelist, new FileDateComparer());
        // Ist nun aufsteigend sortiert
        int lastindex = filelist.Length - 1;
        bool validFound = false;
        while(!validFound && lastindex >= 0)
        {
          // Es müssen vier gleiche Dateien vorhanden sein
          if( filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 1].Name.Substring(0, 18)) &&
              filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 2].Name.Substring(0, 18)) &&
              filelist[lastindex].Name.Substring(0, 18).Equals(filelist[lastindex - 3].Name.Substring(0, 18)) )
          {
            validFound = true;
            dateprefix = filelist[lastindex].Name.Substring(0, filelist[lastindex].Name.IndexOf(FilePrefix, StringComparison.Ordinal));
            logger.Info("Lade die Daten aus diesen Dateien: " + filelist[lastindex].Name);
            if(lastindex != filelist.Length-1)
            {
              logger.Warn("Dies waren nicht die letzten Dateien im Verzeichnis!");
            }
          }
          lastindex--;
        }
        if(!validFound)
        {
          return false;
        }        
      }

      FileStream inputFilestream = null;
      try
      {
        logger.Info("Lese Autoren-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "authors.json");
        StreamReader sr = new StreamReader(inputFilestream);
        string jsonstring = sr.ReadToEnd();
        sr.Close();
        this.authors = JsonConvert.DeserializeObject<Authors.AuthorManager>(jsonstring);
        logger.Info("Lese Post-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "posts.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.posts = JsonConvert.DeserializeObject<Postings.PostingManager>(jsonstring);
        logger.Info("Lese Statistik-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "statistic.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.statistics = JsonConvert.DeserializeObject<DRaumStatistics>(jsonstring);
        logger.Info("Lese Feedback-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "feedback.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.feedbackManager = JsonConvert.DeserializeObject<FeedbackManager>(jsonstring);
        if (this.authors == null || this.posts == null || this.statistics == null || this.feedbackManager == null)
        {
          logger.Error("Beim Deserialisieren wurde eine Klasse als NULL deserialisiert!");
          return false;
        }
        this.posts.transferFromInCheckToToCheck();
        return true;
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Laden der Daten, es wird bei 0 begonnen");
      }
      finally
      {
        if(inputFilestream != null)
        {
          inputFilestream.Close();
        }
      }
      return false;
    }

    private async Task backupData()
    {
      FileStream backupfile = null;
      try
      {
        DirectoryInfo di = new DirectoryInfo(BackupFolder);
        if (!di.Exists)
        {
          di.Create();
        }
        string datestring = this.getDateFileString();
        logger.Info("Schreibe Post-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "posts.json");
        StreamWriter sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.posts, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Autoren-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "authors.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.authors, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Statistik-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "statistic.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.statistics, Formatting.Indented));
        sr.Close();
        logger.Info("Schreibe Feedback-Daten ins Dateisystem");
        backupfile = System.IO.File.Create(BackupFolder + Path.DirectorySeparatorChar + datestring + FilePrefix + "feedback.json");
        sr = new StreamWriter(backupfile);
        await sr.WriteAsync(JsonConvert.SerializeObject(this.feedbackManager, Formatting.Indented));
        sr.Close();
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Schreiben der Daten");
      }
      finally
      {
        if (backupfile != null)
        {
          backupfile.Close();
        }
      }
    }



    // == Methods

    internal async Task shutDown()
    {
      logger.Info("ShutDown läuft, Aufgaben werden abgebrochen, Listener werden beendet, Backup wird erstellt");
      this.cancelTasksSource.Cancel();
      try
      {
        await this.feedbackSendingTask.shutDownTask();
        await this.publishingTask.shutDownTask();
        await this.voteAndFlagTask.shutDownTask();
        await this.statisticCollectionTask.shutDownTask();
        await this.backupTask;
        await this.postAndFeedbackCheckingTask;
      }
      catch (OperationCanceledException e)
      {
        logger.Warn($"{nameof(OperationCanceledException)} erhalten mit der Nachricht: {e.Message}");
      }
      finally
      {
        this.cancelTasksSource.Dispose();
      }

      logger.Info("Listener werden beendet, Backup wird erstellt");
      this.telegramInputBot.StopReceiving();
      this.telegramModerateBot.StopReceiving();
      this.telegramPublishBot.StopReceiving();
      this.telegramFeedbackBot.StopReceiving();

      await this.adminBot.sendMessage(this.adminChatId, "Server ist beendet!");
      await this.backupData();

    }

    private void onReceiveError(object sender, ReceiveErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received an Exception: " + e.ApiRequestException.Message);
    }

    private void onReceiveGeneralError(object sender, ReceiveGeneralErrorEventArgs e)
    {
      logger.Error("Telegram.Bots .NET received a general Exception: " + e.Exception.Message);
    }

    

    private bool canUserVote(long postingId, long authorId, string authorName)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.Runningintestmode)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserVote(postingId, authorId, authorName);
    }

    private bool canUserFlag(long postingId, long authorId, string authorName)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.Runningintestmode)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserFlag(postingId, authorId,authorName);
    }

    private void upvote(long postingId, long authorId, int voteCount)
    {
      this.authors.vote(postingId, authorId);
      this.posts.upvote(postingId, voteCount);
    }

    private void downvote(long postingId, long authorId, int voteCount)
    {
      this.authors.vote(postingId, authorId);
      this.posts.downvote(postingId, voteCount);
    }

    private void flag(long postingId, long authorId)
    {
      this.authors.flag(postingId,authorId);
      this.posts.flag(postingId);
    }

    private string getDateFileString()
    {
      DateTime t = DateTime.Now;
      return t.Year+"_"+t.Month+"_"+t.Day+"_"+t.Hour + "_" + t.Minute;
    }
    
    

    private async Task<bool> acceptPostForPublishing(long postingId)
    {
      Postings.PostingPublishManager.PublishHourType publishType = this.authors.getPublishType(this.posts.getAuthorId(postingId), this.statistics.getPremiumLevelCap());
      string result = "";
      if (publishType != Postings.PostingPublishManager.PublishHourType.None)
      {
        result = this.posts.acceptPost(postingId, publishType);
      }
      if (!result.Equals(""))
      {
        string teaserText = this.posts.getPostingTeaser(postingId);
        long authorId = this.posts.getAuthorId(postingId);
        if (authorId != -1)
        {
          this.authors.publishedSuccessfully(authorId);
          await this.inputBot.sendMessage(authorId, "Der Post ist zum Veröffentlichen freigegeben: " + result + "\r\n\r\nVorschau: " + teaserText);
        }
        else
        {
          try
          {
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Konnte den Userchat zu folgender Posting-ID nicht erreichen (Posting wird aber veröffentlicht): " +
                    postingId + " Textvorschau: " + this.posts.getPostingTeaser(postingId),
              replyMarkup: TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard()
            );
          }
          catch (Exception ex)
          {
            logger.Error(ex, "Fehler beim Benachrichtigen des Moderators über einen Fehler bei Post " + postingId);
          }
          return false;
        }
      }
      else
      {
        await this.adminBot.sendMessageWithKeyboard(this.adminChatId, 
          "Der Post " + postingId + " konnte nicht in die Liste zu veröffentlichender Posts eingefügt werden, FEHLER!", TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard());
        return false;
      }
      return true;
    }


    // == == CALLBACKS == ==

    private async void onFeedbackCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        TelegramUtilities.DRaumCallbackData callbackData = TelegramUtilities.DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModBlockPrefix))
        {
          // verwerfen des feedbacks
          await this.telegramFeedbackBot.EditMessageReplyMarkupAsync(
           chatId: this.feedbackChatId,
           messageId: e.CallbackQuery.Message.MessageId,
           replyMarkup: null
          );
          return;
        }
        if(callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModAcceptPrefix))
        {
          // Antworten auf das Feedback
          this.feedbackManager.enableWaitForFeedbackReply(callbackData.getId());
          await this.telegramFeedbackBot.SendTextMessageAsync(
            chatId: this.feedbackChatId,
            text: "Der nächste eingegebene Text wird an den Autor gesendet"
          );
        }
      }
    }

    private async void onAdminCallback(object sender, CallbackQueryEventArgs e)
    {
      if(e.CallbackQuery.Data != null)
      {
        TelegramUtilities.DRaumCallbackData callbackData = TelegramUtilities.DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.GenericMessageDeletePrefix))
        {
          await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId, this.adminChatId);
          return;
        }
        if(callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModDeleteFlaggedPrefix))
        {
          // Der Admin entscheided den geflaggten Post zu entfernen
          int messageId = this.posts.getMessageId(callbackData.getId());
          int messageIdDaily = this.posts.getMessageIdDaily(callbackData.getId());
          int messageIdWeekly = this.posts.getMessageIdWeekly(callbackData.getId());
          string resultText = "Der Beitrag wurde gelöscht";
          if (messageId != -1)
          {
            if (!this.posts.removePost(callbackData.getId()))
            {
              logger.Error("Konnte den Post "+callbackData.getId()+" nicht aus dem Datensatz löschen");
              resultText = "Konnte nicht aus dem Datensatz gelöscht werden.";
            }
            try
            {
              // Nachricht aus dem D-Raum löschen
              await this.telegramPublishBot.DeleteMessageAsync(
                chatId: this.draumChatId,
                messageId: messageId);
              if (messageIdDaily != -1)
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumDailyChatId,
                  messageId: messageIdDaily);
              }
              if (messageIdWeekly != -1)
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumWeeklyChatId,
                  messageId: messageIdWeekly);
              }
              resultText += "\r\nDer Beitrag wurde aus den Chats gelöscht";
              
            }
            catch (Exception ex)
            {
              logger.Error(ex, "Konnte den Post nicht aus dem Kanal löschen: " + callbackData.getId());
              resultText += "\r\nBeim Löschen aus den Chats gab es Probleme";
            }
          }
          else
          {
            logger.Error("Es konnte keine Message-ID gefunden werden (im Chat) um den Beitrag zu löschen : " + callbackData.getId());
            resultText = "Der Post scheint gar nicht veröffentlicht zu sein";
          }
          // Nachricht aus dem Admin-Chat löschen
          await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId, this.adminChatId);
          // Rückmeldung an Admin
          await this.adminBot.sendMessageWithKeyboard(this.adminChatId, resultText, TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard());
          return;
        }
        if(callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModClearFlagPrefix))
        {
          // Der Admin entscheided, den Flag zurückzunehmen
          if (this.posts.removeFlagFromPost(callbackData.getId()))
          {
            await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId, this.adminChatId);
            await this.adminBot.replyToCallback(e.CallbackQuery.Id, "Flag wurde entfernt");
          }
          else
          {
            logger.Error("Konnte das Flag vom Post nicht entfernen: " + callbackData.getId());
          }
        }
      }
    }

    /// <summary>
    /// Diese Funktion verarbeitet das Drücken der Knöpfe im Moderations-Bot
    /// </summary>
    private async void onModerateCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        TelegramUtilities.DRaumCallbackData callbackData = TelegramUtilities.DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        // ==  Der Moderator akzeptiert den Beitrag
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModAcceptPrefix))
        {          
          if(this.acceptPostForPublishing(callbackData.getId()).Result)
          {
            // Message not needed anymore, delete
            await this.telegramModerateBot.DeleteMessageAsync(
              chatId: this.moderateChatId,
              messageId: e.CallbackQuery.Message.MessageId
            );
            await this.telegramModerateBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Beitrag wird freigegeben",
              showAlert: true);
          }
          else
          {
            await this.telegramModerateBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: "Konnte den Post nicht freigeben...",
              showAlert: true);
          }
          return;
        }
        // ==  Der Moderator will den Beitrag bearbeiten und zurücksenden
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModEditPrefix))
        {
          await this.telegramModerateBot.EditMessageReplyMarkupAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId,
            replyMarkup: TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard()
          );
          this.feedbackManager.waitForModerationText(callbackData.getId());
          await this.telegramModerateBot.AnswerCallbackQueryAsync(
            callbackQueryId: e.CallbackQuery.Id,
            text: "Editierten Beitrag abschicken",
            showAlert: true
          );
          return;
        }
        // ==  Der Moderator lehnt den Beitrag ab
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModBlockPrefix))
        {
          // Nachricht entfernen
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId
          );
          this.feedbackManager.waitForDenyingText(callbackData.getId());
          await this.telegramModerateBot.SendTextMessageAsync(
            chatId: this.moderateChatId,
            text: "Begründung schreiben und abschicken"
          );
          return;
        }
        // TODO Der Moderator blockt den Nutzer für einen Tag/ für eine Woche/ für einen Monat
        if(callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.GenericMessageDeletePrefix))
        {
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId);
          return;
        }
        if(callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModGetNextCheckPostPrefix))
        {
          KeyValuePair<long, string> postingPair = this.posts.getNextPostToCheck();
          if (postingPair.Key != -1)
          {
            try
            {
              await this.telegramModerateBot.SendTextMessageAsync(
                chatId: this.moderateChatId,
                text: postingPair.Value,
                parseMode: ParseMode.Html,
                replyMarkup: TelegramUtilities.Keyboards.getModeratePostKeyboard(postingPair.Key)
              );
            }
            catch (Exception ex)
            {
              logger.Error(ex, "Fehler beim Versenden der Moderationsprüfung von Post " + postingPair.Key + " wird zurück in die Schlange gestellt.");
              if (!this.posts.putBackIntoQueue(postingPair.Key))
              {
                logger.Error("Konnte den Post nicht wieder einfügen, wird gelöscht.");
                Postings.Posting posting = this.posts.removePostFromInCheck(postingPair.Key);
                if(posting != null)
                {
                  await this.inputBot.sendMessage(posting.getAuthorId(),
                    "Dieser Beitrag konnte aufgrund interner Fehler nicht bearbeitet werden:  " +
                    posting.getPostingText() +
                    "\r\n\r\nBitte nochmal probieren. Sollte der Fehler weiterhin bestehen, bitte an einen Administrator wenden.");
                }
                else
                {
                  logger.Error("Der Post konnte nicht gelöscht werden: " + postingPair.Key);
                }
              }
            }
          }
        }
      }
    }

    private async void onPublishCallback(object sender, CallbackQueryEventArgs e)
    {
      try
      {
        if (e.CallbackQuery.Data != null)
        {
          // Auswerten: Vote-up, Vote-down, Flag
          TelegramUtilities.DRaumCallbackData callbackData = TelegramUtilities.DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
          if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.VoteUpPrefix))
          {
            // UPVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              int votecount = this.authors.voteUpAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
              if (votecount != 0)
              {
                this.statistics.increaseInteraction();
                this.authors.updateCredibility(this.posts.getAuthorId(callbackData.getId()), votecount, 0);
                this.upvote(callbackData.getId(), e.CallbackQuery.From.Id, votecount);
                responseText = "Positivstimme erhalten";
              }
              else
              {
                responseText = "Fehler beim Abstimmen!";
              }
            }
            await this.telegramPublishBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: responseText,
              showAlert: true
            );
            return;
          }
          if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.VoteDownPrefix))
          {
            // DOWNVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              int votecount = this.authors.voteDownAndGetCount(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username);
              if (votecount != 0)
              {
                this.statistics.increaseInteraction();
                this.authors.updateCredibility(this.posts.getAuthorId(callbackData.getId()), 0, votecount);
                this.downvote(callbackData.getId(), e.CallbackQuery.From.Id, votecount);
                responseText = "Negativstimme erhalten";
              }
              else
              {
                responseText = "Fehler beim Abstimmen!";
              }
            }
            await this.telegramPublishBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: responseText,
              showAlert: true
            );
            return;
          }
          if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.FlagPrefix))
          {
            // Flagging
            if (!this.authors.isCoolDownOver(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Authors.Author.InteractionCooldownTimer.Flagging))
            {
              TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.CallbackQuery.From.Id,
                e.CallbackQuery.From.Username, Authors.Author.InteractionCooldownTimer.Flagging);
              string msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " +
                                       coolDownTime.TotalMinutes.ToString("0.0") + " Minute(n)";
              if (coolDownTime.TotalMinutes > 180)
              {
                msgCoolDownText = "(Spamvermeidung) Zeit bis zur nächsten Markiermöglichkeit: " +
                                  coolDownTime.TotalHours.ToString("0.0") + " Stunde(n)";
              }
              await this.telegramPublishBot.AnswerCallbackQueryAsync(
                callbackQueryId: e.CallbackQuery.Id,
                text: msgCoolDownText,
                showAlert: true
              );
              return;
            }
            string responseText = "Beitrag bereits markiert oder eigener Post";
            if (this.canUserFlag(callbackData.getId(), e.CallbackQuery.From.Id, e.CallbackQuery.From.Username))
            {
              this.statistics.increaseInteraction();
              this.authors.resetCoolDown(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username, Authors.Author.InteractionCooldownTimer.Flagging);
              this.flag(callbackData.getId(), e.CallbackQuery.From.Id);
              responseText = "Beitrag für Moderation markiert";
            }
            await this.telegramPublishBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: responseText,
              showAlert: true
            );
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Genereller Fehler");
      }
    }

    /// <summary>
    /// Der Nutzer hat einen Beitrag mit Buttons bekommen und muss entscheiden, ob der Beitrag gepostet wird oder nicht
    /// </summary>
    private async void onInputBotCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        TelegramUtilities.DRaumCallbackData callbackData = TelegramUtilities.DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModAcceptPrefix))
        {
          if(this.acceptPostForPublishing(callbackData.getId()).Result)
          {
            await this.inputBot.sendMessage(e.CallbackQuery.From.Id, "Der Beitrag ist angenommen");
            await this.inputBot.removeMessage(e.CallbackQuery.Message.MessageId, e.CallbackQuery.From.Id);
          }
          else
          {
            await this.inputBot.sendMessage(e.CallbackQuery.From.Id, 
              "Post konnte nicht veröffentlicht werden. Probieren Sie es nochmal. Falls es wiederholt fehlschlägt, wenden Sie sich an den Administrator.");
          }
          return;
        }
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModBlockPrefix))
        {
          this.posts.deletePost(callbackData.getId());
          await this.inputBot.removeMessage(e.CallbackQuery.Message.MessageId, e.CallbackQuery.From.Id);
          await this.inputBot.sendMessage(e.CallbackQuery.From.Id, "Der Post wird nicht veröffentlicht und verworfen.");
          return;
        }
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModeWritePrefix))
        {
          await this.inputBot.switchToWriteMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
          return;
        }
        if (callbackData.getPrefix().Equals(TelegramUtilities.Keyboards.ModeFeedbackPrefix))
        {
          await this.inputBot.switchToFeedbackMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
        }
      }
    }


    // == == ON MESSAGES == ==

    /// <summary>
    /// Dies ist der Eingabebot, welcher die Beiträge und Feedback der Nutzer annimmt. Er ist die Schnittstelle zwischen Nutzer und Dem Kanal.
    /// </summary>
    private async void onInputBotMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        // Empfängerprüfung und Spam-Block
        // Dieser Block wirft auch eine Exception, wenn die maximale Nutzerzahl erreicht ist
        try
        {
          if (!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Authors.Author.InteractionCooldownTimer.Default))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Authors.Author.InteractionCooldownTimer.Default);
            await this.inputBot.sendMessage(e.Message.From.Id, 
              "⏳ (Spamvermeidung) Zeit bis zur nächsten Bot-Interaktion: " + coolDownTime.TotalMinutes.ToString("0.0") +
              " Minute(n)");
            return;
          }
        }
        catch(DRaumException dre)
        {
          await this.inputBot.sendMessage(e.Message.From.Id, "Ein Fehler trat auf: " + dre.Message);
          return;
        }
        if (e.Message.Text.Equals("/" + Writecommand))
        {
          await this.inputBot.switchToWriteMode(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id);
          return;
        }
        if (e.Message.Text.Equals("/" + Feedbackcommand))
        {
          await this.inputBot.switchToFeedbackMode(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id);
          return;
        }
        if (this.authors.isFeedbackMode(e.Message.From.Id, e.Message.From.Username) ||
            this.authors.isPostMode(e.Message.From.Id, e.Message.From.Username))
        {
          string text = Utilities.telegramEntitiesToHtml(e.Message.Text, e.Message.Entities);
          await this.inputBot.processTextInput(e.Message.From.Id, e.Message.From.Username, e.Message.Chat.Id, text);
        }
        else
        {
          // Kein Modus
          await this.inputBot.sendMessageWithKeyboard(e.Message.From.Id, NoModeChosen, TelegramUtilities.Keyboards.getChooseInputModeKeyboard());
        }
      }
    }

    private async void onFeedbackMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForFeedbackReply())
        {
          long chatId = this.feedbackManager.processFeedbackReplyAndGetChatId();
          await this.inputBot.sendMessage(chatId,
            "Eine Antwort des Kanalbetreibers auf Ihr Feedback:\r\n\r\n" + e.Message.Text);
          await this.telegramFeedbackBot.SendTextMessageAsync(
            chatId: this.feedbackChatId,
            text: "Feedback-Antwort ist verschickt"
          );
        }
      }
    }

    private async void onModerateMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForModeratedText())
        {
          // Den moderierten Text dem Nutzer zum bestätigen zuschicken.
          Postings.Posting posting = this.posts.getPostingInCheck(this.feedbackManager.getNextModeratedPostId());          
          if (posting != null)
          {
            posting.updateText(e.Message.Text, true);
            await this.inputBot.sendMessageWithKeyboard(posting.getAuthorId(), "MODERIERTER TEXT:\r\n\r\n"+posting.getPostingText(), TelegramUtilities.Keyboards.getAcceptDeclineModeratedPostKeyboard(posting.getPostId()));
            this.feedbackManager.resetProcessModerationText();
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Geänderter Text ist dem Autor zugestellt.",
              replyMarkup: TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard()
            );
            await this.telegramModerateBot.DeleteMessageAsync(
              chatId: this.moderateChatId,
              messageId: e.Message.MessageId);
          }
          else
          {
            logger.Error("Konnte den zu editierenden Post nicht laden: " + this.feedbackManager.getNextModeratedPostId());
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Der zu editierende Post wurde nicht gefunden. Nochmal den Text abschicken. Wenn der Fehler bestehen bleibt, einen Administrator informieren",
              replyMarkup: TelegramUtilities.Keyboards.getGotItDeleteButtonKeyboard()
            );
          }
          return;
        }
        if (this.feedbackManager.isWaitingForDenyText())
        {
          // Die Begründung dem Nutzer zuschicken.
          long postingId = this.feedbackManager.getNextModeratedPostId();
          Postings.Posting posting = this.posts.getPostingInCheck(postingId);          
          if (posting != null)
          {
            string teaser = this.posts.getPostingTeaser(postingId);
            await this.inputBot.sendMessage(posting.getAuthorId(),
              "Der Beitrag wurde durch Moderation abgelehnt. Begründung:\r\n" +
              e.Message.Text + "\r\n\r\nBeitragsvorschau: " + teaser);
            this.feedbackManager.resetProcessModerationText();
            this.posts.discardPost(postingId);
          }
        }
      }
    }


  }
}
