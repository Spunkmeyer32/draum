using System;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot.Types;
using System.Collections;
using System.Text;
using DRaumServerApp.telegram;
using Telegram.Bot.Exceptions;
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
  public class FileDateComparer : IComparer
  {
    public int Compare(object x, object y)
    {
      return ((FileInfo)x).LastWriteTime.CompareTo(((FileInfo)y).LastWriteTime);
    }
  }

  internal class DRaumManager
  {
    #region dynamicmembers
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    // D-Raum Daten
    private AuthorManager authors;
    private PostingManager posts;
    private DRaumStatistics statistics;
    private FeedbackManager feedbackManager;

    private readonly WorldInfoManager worldInfoManager = new WorldInfoManager();

    private FeedbackBufferedSending feedbackBufferedSending;

    // Telegram Bots
    private TelegramBotClient telegramInputBot;
    private TelegramBotClient telegramPublishBot;
    private TelegramBotClient telegramFeedbackBot;
    private TelegramBotClient telegramModerateBot;
    private TelegramBotClient telegramAdminBot;

    private InputBot inputBot;
    private AdminBot adminBot;

    // Chats (mit dem Admin, Moderator, Feedback und zur Veröffentlichung in Kanälen)
    private readonly long feedbackChatId;
    private readonly long moderateChatId;
    private readonly long adminChatId;
    private readonly long draumChatId;
    private readonly long draumDailyChatId;
    private readonly long draumWeeklyChatId;

    private string adminStatisticText = "";
    private readonly HashSet<long> flaggedPostsSent = new HashSet<long>();

    private DateTime lastBackup = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastWorldNewsPost = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopDaily = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastTopWeekly = new DateTime(1999, 1, 1, 9, 0, 0);
    private DateTime lastMessageOfTheDay = new DateTime(1999, 1, 1, 9, 0, 0);

    private Task backupTask;
    private Task publishTask;
    private Task votingFlaggingTask;
    private Task postAndFeedbackCheckingTask;
    private Task statisticCollectTask;

    private string startupinfo = "Keine Info";

    #endregion

    // Statische Optionen
    private const string BackupFolder = "backups";
    private const string FilePrefix = "_draum_";
    private const string Roomname = "d_raum";
    private const string Writecommand = "schreiben";
    private const string Feedbackcommand = "nachricht";

    internal static readonly string modAcceptPrefix = "Y";
    internal static readonly string modEditPrefix = "M";
    internal static readonly string modBlockPrefix = "N";
    internal static readonly string modGetNextCheckPostPrefix = "G";
    internal static readonly string modDeleteFlaggedPrefix = "R";
    internal static readonly string modClearFlagPrefix = "C";
    internal static readonly string modeWritePrefix = "W";
    internal static readonly string modeFeedbackPrefix = "B";
    internal static readonly string genericMessageDeletePrefix = "X";
    internal static readonly string voteUpPrefix = "U";
    internal static readonly string voteDownPrefix = "D";
    internal static readonly string flagPrefix = "F";

    private static readonly Telegram.Bot.Types.Enums.UpdateType[] receivefilterCallbackAndMessage = { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery, Telegram.Bot.Types.Enums.UpdateType.Message };
    private static readonly Telegram.Bot.Types.Enums.UpdateType[] receivefilterCallbackOnly = { Telegram.Bot.Types.Enums.UpdateType.CallbackQuery, Telegram.Bot.Types.Enums.UpdateType.Message };

    private readonly CancellationTokenSource cancelTasksSource = new CancellationTokenSource();

    // Tasks und Intervalle für das regelmäßige Abarbeiten von Aufgaben
    private static readonly int intervalCheckPublishSeconds = 60;
    private static readonly int intervalBackUpDataMinutes = 60;
    private static readonly int intervalVoteAndFlagCountMinutes = 5;
    private static readonly int intervalpostcheckMilliseconds = 1000;
    private static readonly int intervalStatisticCollectionMinutes = 60;

    // Vorgefertigte Texte
    internal static readonly string postIntro = "Schreib-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Posting interpretiert. " +
                                                "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1000\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                "ähnliches\r\n▫️Der Text muss sich im Rahmen der Gesetze bewegen\r\n▫️Keine Urheberrechtsverletzungen\r\n\r\nDer Text wird dann maschinell und ggf. durch " +
                                                "Menschen gegengelesen und wird bei eventuellen Anpassungen in diesem Chat zur Bestätigung durch Sie nochmal abgebildet. " +
                                                "Das Posting wird anonym veröffentlicht. Ihre User-ID wird intern gespeichert.";

    internal static readonly string feedbackIntro = "Feedback-Modus!\r\n\r\nDie nächste Eingabe von Ihnen wird als Feedback für Moderatoren und Kanalbetreiber weitergeleitet. " +
                                                    "Folgende Anforderungen sind zu erfüllen: \r\n\r\n▫️Textlänge zwischen 100 und 1000\r\n▫️Keine URLs\r\n▫️Keine Schimpfworte und " +
                                                    "ähnliches.\r\n\r\nIhre User-ID wird für eine eventuelle Rückmeldung gespeichert.";

    internal static readonly string noModeChosen = "Willkommen beim D-Raum-Input-Bot 🤖.\r\n\r\nEs ist zur Zeit kein Modus gewählt! Mit /" + Writecommand + " schaltet man in den Beitrag-Schreiben-Modus. Mit /" + 
                                                   Feedbackcommand + " kann man in den Feedback-Modus gelangen und den Moderatoren und Kanalbetreibern eine Nachricht "+
                                                   "hinterlassen (Feedback/Wünsche/Kritik).";

    internal static readonly string replyPost = "Danke für den Beitrag ✍️.\r\n\r\nEr wird geprüft und vor der Freigabe nochmal in diesem Chat an Sie verschickt zum gegenlesen. Dies kann dauern, bitte Geduld.";

    internal static readonly string replyFeedback = "Danke für das Feedback 👍.\r\n\r\nEs wird nun von Moderatoren und Kanalbetreiber gelesen. Sie erhalten eventuell hier in diesem Chat eine Rückmeldung.";
    
    internal DRaumManager()
    {
      string testmode = ConfigurationManager.AppSettings["runInTestMode"];
      if (testmode.Equals("true"))
      {
        Utilities.RUNNINGINTESTMODE = true;
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
      this.authors = new AuthorManager();
      logger.Info("Lade Posting-Manager");
      this.posts = new PostingManager();
      logger.Info("Lade Statistik-Manager");
      this.statistics = new DRaumStatistics();
      logger.Info("Lade Feedback-Manager");
      this.feedbackManager = new FeedbackManager();

      if (!this.loadDataFromFiles())
      {
        this.startupinfo = "!!! Server ist ohne Daten gestartet !!!";
        logger.Info("Lade Autor-Manager neu");
        this.authors = new AuthorManager();
        logger.Info("Lade Posting-Manager neu");
        this.posts = new PostingManager();
        logger.Info("Lade Statistik-Manager neu");
        this.statistics = new DRaumStatistics();
        logger.Info("Lade Feedback-Manager neu");
        this.feedbackManager = new FeedbackManager();
      }
      else
      {
        this.startupinfo = "Server ist gestartet";
      }

      this.startupinfo += "\r\nMaximale Autorenzahl:" + AuthorManager.Maxmanagedusers;
    }

    internal void start()
    {
      this.telegramInputBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramInputToken"]);
      this.telegramPublishBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramPublishToken"]);
      this.telegramFeedbackBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramFeedbackToken"]);
      this.telegramModerateBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramModerateToken"]);
      this.telegramAdminBot = new TelegramBotClient(ConfigurationManager.AppSettings["telegramAdminToken"]);

      this.telegramAdminBot.SendTextMessageAsync(chatId: this.adminChatId, text: this.startupinfo +"\r\n"
        + this.statistics.getHardwareInfo());


      Task<Update[]> taskInput = this.telegramInputBot.GetUpdatesAsync();
      Task<Update[]> taskModerate = this.telegramModerateBot.GetUpdatesAsync();
      Task<Update[]> taskPublish = this.telegramPublishBot.GetUpdatesAsync();
      Task<Update[]> taskFeedback = this.telegramFeedbackBot.GetUpdatesAsync();

      this.inputBot = new InputBot(this.authors, this.statistics, this.telegramInputBot, this.posts, this.feedbackManager);
      this.adminBot = new AdminBot(this.authors, this.statistics, this.telegramInputBot, this.posts, this.feedbackManager);

      logger.Info("Setze das Offset bei den Nachrichten, um nicht erhaltene Updates zu löschen");
      Update[] updates = taskInput.Result;
      if (updates.Length > 0)
      {
        this.telegramInputBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskModerate.Result;
      if (updates.Length > 0)
      {
        this.telegramModerateBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskPublish.Result;
      if (updates.Length > 0)
      {
        this.telegramPublishBot.MessageOffset = updates[updates.Length - 1].Id + 1;
      }
      updates = taskFeedback.Result;
      if (updates.Length > 0)
      {
        this.telegramFeedbackBot.MessageOffset = updates[updates.Length - 1].Id + 1;
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

      this.feedbackBufferedSending = new FeedbackBufferedSending(this.feedbackManager, this.telegramFeedbackBot, this.feedbackChatId);

      logger.Info("Starte periodische Aufgaben");
      this.backupTask = this.periodicBackupTask(new TimeSpan(0, intervalBackUpDataMinutes, 0), this.cancelTasksSource.Token);
      this.publishTask = this.periodicPublishTask(new TimeSpan(0, 0, intervalCheckPublishSeconds), this.cancelTasksSource.Token);
      this.votingFlaggingTask = this.periodicVotingFlaggingTask(new TimeSpan(0, intervalVoteAndFlagCountMinutes, 0), this.cancelTasksSource.Token);
      this.postAndFeedbackCheckingTask = this.periodicInputCheckTask(new TimeSpan(0, 0, 0, 0, intervalpostcheckMilliseconds), this.cancelTasksSource.Token);
      this.statisticCollectTask = this.periodicStatisticCollectTask(new TimeSpan(0, intervalStatisticCollectionMinutes, 0), this.cancelTasksSource.Token);
    }


    // ==  Running Tasks
    private async Task periodicVotingFlaggingTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Voting-Flagging-Task ist gestartet");
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
        this.voteFlagTask();
      }
      logger.Info("Voting-Flagging-Task ist beendet");
    }

    private async Task periodicStatisticCollectTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Statistik-Task ist gestartet");
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
        this.statisticCollectionTask();
      }
      logger.Info("Statistik-Task ist beendet");
    }

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

    private async Task periodicPublishTask(TimeSpan interval, CancellationToken cancellationToken)
    {
      logger.Info("Publish-Task ist gestartet");
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
        this.publishingTask();
      }
      logger.Info("Publish-Task ist beendet");
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
    private void statisticCollectionTask()
    {
      try
      {
        this.statistics.switchInteractionInterval();
        checkAndUpdateAdminStatistic();
        this.authors.getMedianAndTopLevel(out var median, out var top);
        this.statistics.updateWritersLevel(top,median);
        long medianVotes = this.posts.getMedianVotes();
        this.statistics.setVotesMedian(medianVotes);
        logger.Info("Statisktik ist nun aktualisiert");
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler beim Verarbeiten der Statistik");
      }
    }

    private void inputCheckTask()
    {
      // Eingehende, zu moderierende Posts bearbeiten
      if (this.posts.getAndResetPostsCheckChangeFlag())
      {
        int messageID = this.feedbackManager.getModerateMessageID();
        int postsToCheck = this.posts.howManyPostsToCheck();
        string message = "Es gibt " + postsToCheck + " Posts zu moderieren.";
        if (messageID == -1)
        {
          // Gibt noch keine Moderator-Message, neu Anlegen
          try
          {
            Message msg = this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: message,
              replyMarkup: Keyboards.getGetNextPostToModerateKeyboard()).Result;
            this.feedbackManager.setModerateMessageID(msg.MessageId);
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
              messageId: messageID,
              text: message,
              replyMarkup: Keyboards.getGetNextPostToModerateKeyboard()).Result;
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Aktualisieren der Moderations-Nachricht");
          }
        }
      }
    }

    private async void voteFlagTask()
    {
      // Posts prüfen, ob Buttons im Chat angepasst werden müssen
      IEnumerable<long> dirtyposts = this.posts.getDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        logger.Debug("Buttons eines Posts (" + postId + ") werden aktualisiert");
        try
        {
          Message msg = await this.telegramPublishBot.EditMessageReplyMarkupAsync(
            chatId: this.draumChatId,
            messageId: this.posts.getMessageId(postId),
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postId), this.posts.getDownVotes(postId), postId));
          this.posts.resetDirtyFlag(postId);
          await Task.Delay(3000);
        }
        catch (Exception ex)
        {
          if (ex is MessageIsNotModifiedException)
          {
            this.posts.resetDirtyFlag(postId);
            logger.Warn("Die Buttons des Posts " + postId + " waren nicht verändert");
          }
          else
          {
            logger.Error(ex, "Beim aktualisieren eines Buttons eines Beitrags (" + postId + ") trat ein Fehler auf.");
          }
        }
      }

      // Posts prüfen, ob Texte im Chat angepasst werden müssen
      dirtyposts = this.posts.getTextDirtyPosts();
      foreach (long postId in dirtyposts)
      {
        logger.Debug("Text eines Posts ("+postId+") wird aktualisiert");
        try
        {
          Message msg = await this.telegramPublishBot.EditMessageTextAsync(
            chatId: this.draumChatId,
            parseMode: ParseMode.Html,
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postId), this.posts.getDownVotes(postId), postId),
            messageId: this.posts.getMessageId(postId),
            text: this.buildPostingText(postId));
          this.posts.resetTextDirtyFlag(postId);
          await Task.Delay(3000);
        }
        catch (Exception ex)
        {
          if (ex is MessageIsNotModifiedException)
          {
            this.posts.resetTextDirtyFlag(postId);
            logger.Warn("Der Text des Posts " + postId + " ist nicht verändert");
          }
          else
          {
            logger.Error(ex, "Beim aktualisieren eines Textes eines Beitrags (" + postId + ") trat ein Fehler auf.");
          }
        }
      }



      // Prüfen, ob ein Flag vorliegt und dem Admin melden
      IEnumerable<long> flaggedPosts = this.posts.getFlaggedPosts();
      HashSet<long> flaggedSentOld = new HashSet<long>();
      foreach (long postId in this.flaggedPostsSent)
      {
        flaggedSentOld.Add(postId);
      }
      this.flaggedPostsSent.Clear();
      foreach (long postId in flaggedPosts)
      {
        if (!flaggedSentOld.Contains(postId))
        {
          // getText and send to Admin
          string msgText = "Dieser Post wurde " + this.posts.getFlagCountOfPost(postId) + "-Mal geflaggt!!! \r\n" + this.posts.getPostingText(postId);
          Message msg = await this.adminBot.sendMessageWithKeyboard(this.adminChatId, msgText,
            Keyboards.getFlaggedPostModKeyboard(postId));
          if (msg == null)
          {
            logger.Error("Beim senden eines geflaggten Beitrags trat ein Fehler auf.");
          }
          else
          {
            this.flaggedPostsSent.Add(postId);
          }
        }
        else
        {
          this.flaggedPostsSent.Add(postId);
        }
      }


      // Update the Status of top-posts
      this.posts.updateTopPostStatus(this.statistics);
    }
    
    private async Task backUpTask()
    {
      try
      {
        logger.Debug("Anhalten für Backup");
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
        logger.Debug("Backup erledigt, weitermachen");
        this.lastBackup = DateTime.Now;
        this.checkAndUpdateAdminStatistic();
      }
      catch(Exception e)
      {
        logger.Error(e, "Fehler im Backup-Task");
      }
    }

    private async void publishingTask()
    {
      bool skip = false;
      // Message Of The Day
      if ((DateTime.Now - this.lastMessageOfTheDay).TotalHours > 24.0)
      {
        if (this.lastMessageOfTheDay.Year <= 2000)
        {
          if (!Utilities.RUNNINGINTESTMODE)
          {
            skip = true;
          }
        }
        this.lastMessageOfTheDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 30, 0);
        logger.Info("Nächste MOTD am " + this.lastMessageOfTheDay.AddHours(24).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          string motd = "== Service Post ==\r\n\r\n✍️ Möchten Sie selbst auch hier schreiben?\r\nDann verwenden Sie dazu den Eingabe-Bot:\r\n\r\n  🤖  @d_raum_input_bot  🤖";
          try
          {
            await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumChatId,
              text: motd);
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Posten der MOTD");
          }
        }
      }



      skip = false;
      // News-Post
      if( (DateTime.Now - this.lastWorldNewsPost).TotalHours > 24.0 )
      {
        if (this.lastWorldNewsPost.Year <= 2000)
        {
          if (!Utilities.RUNNINGINTESTMODE)
          {
            skip = true;
          }
        }
        this.lastWorldNewsPost = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
        logger.Info("Nächste News am " + this.lastWorldNewsPost.AddHours(24).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          string news = this.worldInfoManager.getInfoStringForChat();
          try
          {
            await this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumChatId,
              text: news);
          }
          catch (Exception e)
          {
            logger.Error(e, "Fehler beim Posten der News");
          }
        }
      }

      skip = false;
      // Top-Daily
      if ((DateTime.Now - this.lastTopDaily).TotalHours > 24.0)
      {
        if (this.lastTopDaily.Year <= 2000)
        {
          if (!Utilities.RUNNINGINTESTMODE)
          {
            skip = true;
          }
        }
        this.lastTopDaily = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
        logger.Info("Nächste Top Tages Posts am " + this.lastTopDaily.AddHours(24).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          List<long> topPosts = this.posts.getDailyTopPostsFromYesterday();
          foreach (long postId in topPosts)
          {
            logger.Debug("Es soll folgender Post in Top-Daily veröffentlicht werden: " + postId);
            Message result = this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumDailyChatId,
              parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
              text: this.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
            ).Result;
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (daily,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setDailyChatMsgId(postId, result.MessageId);
            }
            await Task.Delay(3000);
          }


          List<long> deleteablePosts = this.posts.getPostsToDelete();
          foreach (long postId in deleteablePosts)
          {
            logger.Debug("Es soll folgender Post gelöscht werden (abgelaufen): " + postId);
            long messageId = this.posts.getMessageId(postId);
            long messageDailyId = this.posts.getMessageIdDaily(postId);
            long messageWeeklyId = this.posts.getMessageIdWeekly(postId);
            if (messageId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumChatId,
                  messageId: (int) messageId);
              }
              catch (Exception ex)
              {
                logger.Error(ex,"Fehler beim Löschen aus dem D-Raum");
              }
            }
            if (messageDailyId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumDailyChatId,
                  messageId: (int)messageDailyId);
              }
              catch (Exception ex)
              {
                logger.Error(ex,"Fehler beim Löschen aus dem D-Raum-Täglich");
              }
            }
            if (messageWeeklyId != -1)
            {
              try
              {
                await this.telegramPublishBot.DeleteMessageAsync(
                  chatId: this.draumWeeklyChatId,
                  messageId: (int)messageWeeklyId);
              }
              catch (Exception ex)
              {
                logger.Error(ex,"Fehler beim Löschen aus dem D-Raum-Wöchentlich");
              }
            }
            this.posts.deletePost(postId);
            await Task.Delay(3000);
          }

        }
      }

      skip = false;
      // Top-Weekly
      if ((DateTime.Now - this.lastTopWeekly).TotalDays > 7.0)
      {
        DayOfWeek currentDay = DateTime.Now.DayOfWeek;
        int daysTillCurrentDay = currentDay - DayOfWeek.Saturday;
        if(daysTillCurrentDay < 0)
        {
          daysTillCurrentDay += 7;
        }
        DateTime currentWeekStartDate = DateTime.Now.AddDays(-daysTillCurrentDay);
        if (this.lastTopWeekly.Year <= 2000)
        {
          if (!Utilities.RUNNINGINTESTMODE)
          {
            skip = true;
          }
        }
        this.lastTopWeekly = new DateTime(currentWeekStartDate.Year, currentWeekStartDate.Month, currentWeekStartDate.Day, 9, 0, 0);
        logger.Info("Nächste Top Wochen Posts am " + this.lastTopWeekly.AddDays(7).ToString(Utilities.usedCultureInfo));
        if (!skip)
        {
          List<long> topPosts = this.posts.getWeeklyTopPostsFromLastWeek();
          foreach (long postId in topPosts)
          {
            logger.Debug("Es soll folgender Post in Top-Weekly veröffentlicht werden: " + postId);
            Message result = this.telegramPublishBot.SendTextMessageAsync(
              chatId: this.draumWeeklyChatId,
              parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
              text: this.buildPostingTextForTopTeaser(postId),
              replyMarkup: Keyboards.getTopPostLinkKeyboard(this.posts.getMessageId(postId), DRaumManager.Roomname)
            ).Result;
            if (result == null || result.MessageId == 0)
            {
              logger.Error("Fehler beim Publizieren des Posts (weekly,keine msg ID) bei Post " + postId);
            }
            else
            {
              this.posts.setWeeklyChatMsgId(postId, result.MessageId);
            }

            await Task.Delay(3000);
          }
        }
      }

      long postID = -1;
      bool fail = false;
      try
      {
        Posting toPublish = this.posts.tryPublish();
        if (toPublish != null)
        {
          postID = toPublish.getPostID();
          // Ab in den D-Raum damit
          logger.Debug("Es soll folgender Post veröffentlicht werden: " + postID);
          Message result = this.telegramPublishBot.SendTextMessageAsync(
            chatId: this.draumChatId,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            text: this.buildPostingText(postID),
            replyMarkup: Keyboards.getPostKeyboard(this.posts.getUpVotes(postID), this.posts.getDownVotes(postID), postID)
          ).Result;
          if (result == null || result.MessageId == 0)
          {
            logger.Error("Fehler beim Publizieren des Posts (keine msg ID) bei Post " + postID);
          }
          else
          {
            toPublish.resetTextDirtyFlag();
            toPublish.resetDirtyFlag();
            toPublish.setChatMessageID(result.MessageId);
          }
        }
      }
      catch (Exception e)
      {
        logger.Error(e, "(Exception)Fehler beim Publizieren des Posts: " + postID);
        await this.adminBot.sendMessage(this.adminChatId, "Fehler beim Publizieren des Posts: " + postID);
        fail = true;
      }
      if (fail)
      {
        // TODO den Post wieder einreihen
      }
    }



    // == Persistancy
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
        this.authors = JsonConvert.DeserializeObject<AuthorManager>(jsonstring);
        logger.Info("Lese Post-Daten aus dem Dateisystem");
        inputFilestream = System.IO.File.OpenRead(BackupFolder + Path.DirectorySeparatorChar + dateprefix + FilePrefix + "posts.json");
        sr = new StreamReader(inputFilestream);
        jsonstring = sr.ReadToEnd();
        sr.Close();
        this.posts = JsonConvert.DeserializeObject<PostingManager>(jsonstring);
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
        await sr.WriteAsync(JsonConvert.SerializeObject(this.posts,Formatting.Indented));
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

    internal async void shutDown()
    {
      logger.Info("ShutDown läuft, Aufgaben werden abgebrochen, Listener werden beendet, Backup wird erstellt");
      this.cancelTasksSource.Cancel();
      this.feedbackBufferedSending.shutDownTask();
      try
      {
        await this.backupTask;
        await this.publishTask;
        await this.postAndFeedbackCheckingTask;
        await this.votingFlaggingTask;
        await this.statisticCollectTask;
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
      this.telegramAdminBot.SendTextMessageAsync(chatId: this.adminChatId, text: "Server ist beendet!").Wait();
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

    private async void checkAndUpdateAdminStatistic()
    {
      string newtext = "Interaktionen im letzten Intervall: " + this.statistics.getLastInteractionIntervalCount()+"\r\n";
      newtext += "Letztes Backup: " + this.lastBackup.ToString(Utilities.usedCultureInfo)+"\r\n";
      newtext += "Hardware-Information: " + this.statistics.getHardwareInfo();
      if (!newtext.Equals(this.adminStatisticText))
      {
        this.adminStatisticText = newtext;
        int msgId = this.feedbackManager.getAdminStatisticMessageID();
        if (msgId == -1)
        {
          Message msg = await this.adminBot.sendMessage(this.adminChatId, this.adminStatisticText);
          if (msg == null)
          {
            this.feedbackManager.setAdminStatisticMessageID(-1);
            logger.Error("Fehler beim Senden der Statistiknachricht an den Admin");
          }
          else
          {
            this.feedbackManager.setAdminStatisticMessageID(msg.MessageId);
          }
        }
        else
        {
          await this.adminBot.editMessage(this.adminChatId, msgId, this.adminStatisticText);
        }
      }
    }

    private bool canUserVote(long postingId, long authorId)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.RUNNINGINTESTMODE)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserVote(postingId, authorId);
    }

    private bool canUserFlag(long postingId, long authorId)
    {
      // Wenn der Nutzer Autor ist, kann er nicht voten
      if (!Utilities.RUNNINGINTESTMODE)
      {
        if (this.posts.isAuthor(postingId, authorId))
        {
          return false;
        }
      }
      return this.authors.canUserFlag(postingId, authorId);
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
    
    private string buildPostingText(long postingId)
    {
      StringBuilder sb = new StringBuilder();
      if (this.posts.isTopPost(postingId))
      {
        sb.Append("<b>🔈 == TOP-POST Nr. ");
        sb.Append(postingId);
        sb.Append(" == 🔈</b>\r\n\r\n");
      }
      else
      {
        sb.Append("<b>Post Nr. ");
        sb.Append(postingId);
        sb.Append("</b>\r\n\r\n");
      }
      sb.Append(this.posts.getPostingText(postingId));
      sb.Append("\r\n\r\n");
      sb.Append(this.authors.getAuthorPostText(this.posts.getAuthorId(postingId)));
      sb.Append("\r\n");
      sb.Append(this.posts.getPostingStatisticText(postingId));
      return sb.ToString();
    }

    private string buildPostingTextForTopTeaser(long postingId)
    {
      StringBuilder sb = new StringBuilder();
      if (this.posts.isTopPost(postingId))
      {
        sb.Append("<b>🔈 == TOP-POST Nr. ");
        sb.Append(postingId);
        sb.Append(" == 🔈</b>\r\n\r\n");
      }
      else
      {
        sb.Append("<b>Post Nr. ");
        sb.Append(postingId);
        sb.Append("</b>\r\n\r\n");
      }
      sb.Append(this.posts.getPostingText(postingId).Substring(0, 60));
      sb.Append(" [...]");
      sb.Append("\r\n\r\n");
      sb.Append(this.authors.getAuthorPostText(this.posts.getAuthorId(postingId)));
      sb.Append("\r\n");
      sb.Append(this.posts.getPostingStatisticText(postingId));
      return sb.ToString();
    }

    private async Task<bool> acceptPostForPublishing(long postingID)
    {
      PostingPublishManager.publishHourType publishType = this.authors.getPublishType(this.posts.getAuthorId(postingID), this.statistics.getPremiumLevelCap());
      string result = "";
      if (publishType != PostingPublishManager.publishHourType.NONE)
      {
        result = this.posts.acceptPost(postingID, publishType);
      }
      if (!result.Equals(""))
      {
        string teaserText = this.posts.getPostingTeaser(postingID);
        long authorId = this.posts.getAuthorId(postingID);
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
                    postingID + " Textvorschau: " + this.posts.getPostingTeaser(postingID),
              replyMarkup: Keyboards.getGotItDeleteButtonKeyboard()
            );
          }
          catch (Exception ex)
          {
            logger.Error(ex, "Fehler beim Benachrichtigen des Moderators über einen Fehler bei Post " + postingID);
          }
          return false;
        }
      }
      else
      {
        await this.adminBot.sendMessageWithKeyboard(this.adminChatId, 
          "Der Post " + postingID + " konnte nicht in die Liste zu veröffentlichender Posts eingefügt werden, FEHLER!",
          Keyboards.getGotItDeleteButtonKeyboard());
        return false;
      }
      return true;
    }


    // == == CALLBACKS == ==

    private async void onFeedbackCallback(object sender, CallbackQueryEventArgs e)
    {
      if (e.CallbackQuery.Data != null)
      {
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(modBlockPrefix))
        {
          // verwerfen des feedbacks
          await this.telegramFeedbackBot.EditMessageReplyMarkupAsync(
           chatId: this.feedbackChatId,
           messageId: e.CallbackQuery.Message.MessageId,
           replyMarkup: null
          );
          return;
        }
        if(callbackData.getPrefix().Equals(modAcceptPrefix))
        {
          // Antworten auf das Feedback
          this.feedbackManager.enableWaitForFeedbackReply(callbackData.getId());
          await this.telegramFeedbackBot.SendTextMessageAsync(
            chatId: this.feedbackChatId,
            text: "Der nächste eingegebene Text wird an den Autor gesendet"
          );
          return;
        }
      }
    }

    private async void onAdminCallback(object sender, CallbackQueryEventArgs e)
    {
      if(e.CallbackQuery.Data != null)
      {
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(genericMessageDeletePrefix))
        {
          await this.adminBot.removeMessage(e.CallbackQuery.Message.MessageId, this.adminChatId);
          return;
        }
        if(callbackData.getPrefix().Equals(modDeleteFlaggedPrefix))
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
          await this.adminBot.sendMessageWithKeyboard(this.adminChatId, resultText, Keyboards.getGotItDeleteButtonKeyboard());
          return;
        }
        if(callbackData.getPrefix().Equals(modClearFlagPrefix))
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
          return;
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
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        // ==  Der Moderator akzeptiert den Beitrag
        if (callbackData.getPrefix().Equals(modAcceptPrefix))
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
        if (callbackData.getPrefix().Equals(modEditPrefix))
        {
          await this.telegramModerateBot.EditMessageReplyMarkupAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId,
            replyMarkup: Keyboards.getGotItDeleteButtonKeyboard()
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
        if (callbackData.getPrefix().Equals(modBlockPrefix))
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
        if(callbackData.getPrefix().Equals(genericMessageDeletePrefix))
        {
          await this.telegramModerateBot.DeleteMessageAsync(
            chatId: this.moderateChatId,
            messageId: e.CallbackQuery.Message.MessageId);
          return;
        }
        if(callbackData.getPrefix().Equals(modGetNextCheckPostPrefix))
        {
          KeyValuePair<long, string> postingPair = this.posts.getNextPostToCheck();
          if (postingPair.Key != -1)
          {
            try
            {
              Message msg = this.telegramModerateBot.SendTextMessageAsync(
                chatId: this.moderateChatId,
                text: postingPair.Value,
                parseMode: ParseMode.Html,
                replyMarkup: Keyboards.getModeratePostKeyboard(postingPair.Key)
              ).Result;
            }
            catch (Exception ex)
            {
              logger.Error(ex, "Fehler beim Versenden der Moderationsprüfung von Post " + postingPair.Key + " wird zurück in die Schlange gestellt.");
              if (!this.posts.putBackIntoQueue(postingPair.Key))
              {
                logger.Error("Konnte den Post nicht wieder einfügen, wird gelöscht.");
                Posting posting = this.posts.removePostFromInCheck(postingPair.Key);
                if(posting != null)
                {
                  await this.inputBot.sendMessage(posting.getAuthorID(),
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
          return;
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
          DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
          if (callbackData.getPrefix().Equals(voteUpPrefix))
          {
            // UPVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id))
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
          if (callbackData.getPrefix().Equals(voteDownPrefix))
          {
            // DOWNVOTE
            string responseText = "Stimme bereits abgegeben oder eigener Post";
            if (this.canUserVote(callbackData.getId(), e.CallbackQuery.From.Id))
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
          if (callbackData.getPrefix().Equals(flagPrefix))
          {
            // Flagging
            if (!this.authors.isCoolDownOver(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
              Author.InteractionCooldownTimer.FLAGGING))
            {
              TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.CallbackQuery.From.Id,
                e.CallbackQuery.From.Username, Author.InteractionCooldownTimer.FLAGGING);
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
            if (this.canUserFlag(callbackData.getId(), e.CallbackQuery.From.Id))
            {
              this.statistics.increaseInteraction();
              this.authors.resetCoolDown(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
                Author.InteractionCooldownTimer.FLAGGING);
              this.flag(callbackData.getId(), e.CallbackQuery.From.Id);
              responseText = "Beitrag für Moderation markiert";
            }
            await this.telegramPublishBot.AnswerCallbackQueryAsync(
              callbackQueryId: e.CallbackQuery.Id,
              text: responseText,
              showAlert: true
            );
            return;
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
        DRaumCallbackData callbackData = DRaumCallbackData.parseCallbackData(e.CallbackQuery.Data);
        if (callbackData.getPrefix().Equals(modAcceptPrefix))
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
        if (callbackData.getPrefix().Equals(modBlockPrefix))
        {
          this.posts.deletePost(callbackData.getId());
          await this.inputBot.removeMessage(e.CallbackQuery.Message.MessageId, e.CallbackQuery.From.Id);
          await this.inputBot.sendMessage(e.CallbackQuery.From.Id, "Der Post wird nicht veröffentlicht und verworfen.");
          return;
        }
        if (callbackData.getPrefix().Equals(modeWritePrefix))
        {
          await this.inputBot.switchToWriteMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
          return;
        }
        if (callbackData.getPrefix().Equals(modeFeedbackPrefix))
        {
          await this.inputBot.switchToFeedbackMode(e.CallbackQuery.From.Id, e.CallbackQuery.From.Username,
            e.CallbackQuery.From.Id);
          return;
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
          if (!this.authors.isCoolDownOver(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT))
          {
            TimeSpan coolDownTime = this.authors.getCoolDownTimer(e.Message.From.Id, e.Message.From.Username, Author.InteractionCooldownTimer.DEFAULT);
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
          await this.inputBot.sendMessageWithKeyboard(e.Message.From.Id, noModeChosen, Keyboards.getChooseInputModeKeyboard());
        }
      }
    }

    private async void onFeedbackMessage(object sender, MessageEventArgs e)
    {
      if (e.Message.Text != null)
      {
        if (this.feedbackManager.isWaitingForFeedbackReply())
        {
          long chatId = this.feedbackManager.processFeedbackReplyAndGetChatID();
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
          Posting posting = this.posts.getPostingInCheck(this.feedbackManager.getNextModeratedPostID());          
          if (posting != null)
          {
            posting.updateText(e.Message.Text, true);
            await this.inputBot.sendMessageWithKeyboard(posting.getAuthorID(), "MODERIERTER TEXT:\r\n\r\n"+posting.getPostingText(),
              Keyboards.getAcceptDeclineModeratedPostKeyboard(posting.getPostID()));
            this.feedbackManager.resetProcessModerationText();
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Geänderter Text ist dem Autor zugestellt.",
              replyMarkup: Keyboards.getGotItDeleteButtonKeyboard()
            );
            await this.telegramModerateBot.DeleteMessageAsync(
              chatId: this.moderateChatId,
              messageId: e.Message.MessageId);
          }
          else
          {

            logger.Error("Konnte den zu editierenden Post nicht laden: " + this.feedbackManager.getNextModeratedPostID());
            await this.telegramModerateBot.SendTextMessageAsync(
              chatId: this.moderateChatId,
              text: "Der zu editierende Post wurde nicht gefunden. Nochmal den Text abschicken. Wenn der Fehler bestehen bleibt, einen Administrator informieren",
              replyMarkup: Keyboards.getGotItDeleteButtonKeyboard()
            );
          }
          return;
        }
        if (this.feedbackManager.isWaitingForDenyText())
        {
          // Die Begründung dem Nutzer zuschicken.
          long postingID = this.feedbackManager.getNextModeratedPostID();
          Posting posting = this.posts.getPostingInCheck(postingID);          
          if (posting != null)
          {
            string teaser = this.posts.getPostingTeaser(postingID);
            await this.inputBot.sendMessage(posting.getAuthorID(),
              "Der Beitrag wurde durch Moderation abgelehnt. Begründung:\r\n" +
              e.Message.Text + "\r\n\r\nBeitragsvorschau: " + teaser);
            this.feedbackManager.resetProcessModerationText();
            this.posts.discardPost(postingID);
          }
          return;
        }
      }
    }


  }
}
