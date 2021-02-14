using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Telegram.Bot.Types;
using static DRaumServerApp.Author;

namespace DRaumServerApp
{
  class AuthorManager
  {
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    public static int MAXMANAGEDUSERS = 25;

    [JsonProperty]
    private ConcurrentDictionary<long, Author> authors;    

    internal AuthorManager()
    {
      this.authors = new ConcurrentDictionary<long, Author>();
    }

    internal AuthorManager(IDictionary<long,Author> authors)
    {
      this.authors = new ConcurrentDictionary<long, Author>();
      foreach(KeyValuePair<long,Author> pair in authors)
      {
        if(!this.authors.TryAdd(pair.Key, pair.Value))
        {
          logger.Error("Konnte folgende Nutzerdaten nicht in die Liste einfügen: " + pair.Key + " , " + pair.Value);
        }
      }
    }

    private Author getAuthor(long authorID)
    {
      if (this.authors.ContainsKey(authorID))
      {
        return this.authors[authorID];
      }
      return null;
    }

    private Author getAuthor(long authorID, String externalName) 
    {
      if (this.authors.ContainsKey(authorID))
      {
        return this.authors[authorID];
      }
      else
      {
        if (this.authors.Count < MAXMANAGEDUSERS)
        {
          if (externalName == null)
          {
            externalName = "";
          }
          Author newAuthor = new Author(authorID, externalName);
          if (this.authors.TryAdd(authorID, newAuthor))
          {
            logger.Info("Neuer Autor: " + externalName + " (" + authorID + ")");
            return newAuthor;
          }
          else
          {
            throw new DRaumException("Aufgrund eines Fehlers konnte der Nutzer nicht hinzugefügt werden. Bitter später erneut probieren oder einen Administrator kontaktieren.");
          }
        }
        else
        {
          throw new DRaumException("Maximale Nutzeranzahl erreicht");
        }
      }
    }

    internal void getMedianAndTopLevel(out int medianOut, out int topOut)
    {
      // alle Autoren Prüfen und Median und Top ermitteln
      int toplevel = 0;
      ArrayList levellist = new ArrayList();
      int temp = 0;
      foreach(Author author in this.authors.Values)
      {
        temp = author.getUserLevel();
        levellist.Add(temp);
        if(temp > toplevel)
        {
          toplevel = temp;
        }
      }
      Array target = levellist.ToArray();
      Array.Sort(target);
      if(target.Length == 0)
      {
        medianOut = 0;
        topOut = 0;
        return;
      }
      int median = (int)target.GetValue(target.Length / 2);
      medianOut = median;
      topOut = toplevel;      
    }

    internal void setPostMode(long id, String externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.setPostMode();
    }

    internal PostingPublishManager.publishHourType getPublishType(long authorID, int premiumLevelCap)
    {
      Author author = this.getAuthor(authorID);
      if(author != null)
      {
        return author.getPublishType(premiumLevelCap);
      }
      return PostingPublishManager.publishHourType.NONE;
    }

    internal bool isPostMode(long id, String externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author.isInPostMode();
    }

    internal void unsetModes(int id, String externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.unsetModes();
    }

    internal void setFeedbackMode(int id, String externalName)
    {
      Author author = this.getAuthor(id, externalName);
      author.setFeedbackMode();
    }

    internal bool isFeedbackMode(int id, String externalName)
    {
      Author author = this.getAuthor(id, externalName);
      return author.isInFeedbackMode();
    }

    internal bool isCoolDownOver(int id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author.coolDownOver(timerType);
    }

    internal void resetCoolDown(int id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      author.resetCoolDown(timerType);
    }

    internal TimeSpan getCoolDownTimer(int id, string externalName, InteractionCooldownTimer timerType)
    {
      Author author = this.getAuthor(id, externalName);
      return author.getCoolDownTimer(timerType);
    }

    internal string getAuthorPostText(long authorID)
    {
      Author author = this.getAuthor(authorID);
      if(author == null)
      {
        return "<i>Schreiber/in nicht gefunden!</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
      }
      return "<i>" + author.getUserInfo() + "</i>\r\n<i>Verfasst im D-Raum https://t.me/d_raum </i>";
    }

    internal void updateCredibility(long authorID, long receivedUpVotes, long receivedDownVotes)
    {
      Author author = this.getAuthor(authorID);
      if (author != null)
      {
        author.updateCredibility(receivedUpVotes, receivedDownVotes);
      }
      else
      {
        logger.Warn("Konnte votes dem Nutzer mit der ID " + authorID + " nicht zuordnen");
      }
    }

    internal void publishedSuccessfully(long authorID)
    {
      Author author = this.getAuthor(authorID);
      if (author != null)
      {
        author.publishedSuccessfully();
      }
      else
      {
        logger.Warn("Konnte die Veröffentlichung dem Nutzer mit der ID " + authorID + " nicht gutschreiben");
      }
    }

    internal int voteUpAndGetCount(long authorID, string username)
    {
      Author author = this.getAuthor(authorID, username);
      if (author != null)
      {
        return author.voteUpAndGetCount();
      }
      return 0;
    }

    internal int voteDownAndGetCount(long authorID, string username)
    {
      Author author = this.getAuthor(authorID,username);
      if (author != null)
      {
        return author.voteDownAndGetCount();
      }
      return 0;
    }

   
  }
    
}
