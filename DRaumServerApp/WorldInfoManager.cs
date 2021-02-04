using RestSharp;
using System;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Configuration;

namespace DRaumServerApp
{
  class WorldInfoManager
  {
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    private float goldPriceInEuro = 0.0f;
    private float silverPriceInEuro = 0.0f;
    private float unleadedFuelPriceInEuro = 0.0f;
    private float unleadedBioFuelPriceInEuro = 0.0f;
    private float dieselFuelPriceInEuro = 0.0f;
    private float bitcoinInEuro = 0.0f;

    private static String trendUp = "↗️";
    private static String trendDown = "↘️";
    private static String trendEqual = "➡️";

    private String goldPriceInEuroTrend =  trendEqual;
    private String silverPriceInEuroTrend = trendEqual;
    private String unleadedFuelPriceInEuroTrend = trendEqual;
    private String unleadedBioFuelPriceInEuroTrend = trendEqual;
    private String dieselFuelPriceInEuroTrend = trendEqual;
    private String bitcoinInEuroTrend = trendEqual;

    private String infoString = "";
    private DateTime lastCheck = DateTime.Now;

    

    public String getInfoStringForChat()
    {
      if ((DateTime.Now - lastCheck).TotalHours >= 24.0 || this.infoString.Length < 10)
      {
        lastCheck = DateTime.Now;
        // Daten einholen und in infoString speichern
        try
        {
          this.getBitcoin();
          this.getFuelprice();
          this.getGold();
          this.getSilver();
          if (this.infoString.Length < 10)
          {
            // Keine alten Daten, keine Trends anzeigen
            this.infoString = "== Tagesinfo ==\r\n\r\n🟡 Gold:  " + this.goldPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n" +
              "⚪️ Silber:  " + this.silverPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n" +
              "⛽️ Super-Benzin:  " + this.unleadedFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n" +
              "⛽️ Super-E10:  " + this.unleadedBioFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n" +
              "⛽️ Diesel:  " + this.dieselFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n" +
              "💰 Bitcoin:  " + this.bitcoinInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "\r\n\r\n" +
              "🔈 Aktualisierung am " + this.lastCheck.ToShortDateString() + " um " + this.lastCheck.ToShortTimeString() + " Uhr";
          }
          else
          {
            this.infoString = "== Tagesinfo ==\r\n\r\n🟡 Gold:  " + this.goldPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.goldPriceInEuroTrend + "\r\n" +
              "⚪️ Silber:  " + this.silverPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.silverPriceInEuroTrend + "\r\n" +
              "⛽️ Super-Benzin:  " + this.unleadedFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.unleadedFuelPriceInEuroTrend + "\r\n" +
              "⛽️ Super-E10:  " + this.unleadedBioFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.unleadedBioFuelPriceInEuroTrend + "\r\n" +
              "⛽️ Diesel:  " + this.dieselFuelPriceInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.dieselFuelPriceInEuroTrend + "\r\n" +
              "💰 Bitcoin:  " + this.bitcoinInEuro.ToString("C", CultureInfo.CreateSpecificCulture("de-DE")) + "  " + this.bitcoinInEuroTrend + "\r\n\r\n" +
              "🔈 Aktualisierung am " + this.lastCheck.ToShortDateString() + " um " + this.lastCheck.ToShortTimeString() + " Uhr";
          }
        }
        catch(Exception e)
        {
          logger.Error(e, "Fehler beim holen der Informationen der WEB-APIs");
        }
      } 
      return this.infoString;
    }


    private void getGold()
    {
      var client = new RestClient("https://www.goldapi.io/api/XAU/EUR");
      var request = new RestRequest(Method.GET);
      request.AddHeader("x-access-token", ConfigurationManager.AppSettings["infoAPIKeyGold"] );
      request.AddHeader("Content-Type", "application/json");
      IRestResponse response = client.Execute(request);
      JObject jsonResponse = JObject.Parse(response.Content);
      float temp = jsonResponse.GetValue("price").ToObject<float>();
      if (this.goldPriceInEuro < temp)
      {
        this.goldPriceInEuroTrend = trendUp;
      }
      else
      {
        if (this.goldPriceInEuro > temp)
        {
          this.goldPriceInEuroTrend = trendDown;
        }
        else
        {
          this.goldPriceInEuroTrend = trendEqual;
        }
      }
      this.goldPriceInEuro = temp;
    }

    private void getSilver()
    {
      var client = new RestClient("https://www.goldapi.io/api/XAG/EUR");
      var request = new RestRequest(Method.GET);
      request.AddHeader("x-access-token", ConfigurationManager.AppSettings["infoAPIKeyGold"] );
      request.AddHeader("Content-Type", "application/json");
      IRestResponse response = client.Execute(request);
      JObject jsonResponse = JObject.Parse(response.Content);
      float temp = jsonResponse.GetValue("price").ToObject<float>();
      if (this.silverPriceInEuro < temp)
      {
        this.silverPriceInEuroTrend = trendUp;
      }
      else
      {
        if (this.silverPriceInEuro > temp)
        {
          this.silverPriceInEuroTrend = trendDown;
        }
        else
        {
          this.silverPriceInEuroTrend = trendEqual;
        }
      }
      this.silverPriceInEuro = temp;
    }

    private void getFuelprice()
    {
      String url = "https://creativecommons.tankerkoenig.de/json/prices.php?ids="+ 
        ConfigurationManager.AppSettings["infoAPIIDFuel"] + "&apikey="+ 
        ConfigurationManager.AppSettings["infoAPIKeyFuel"];
    var client = new RestClient(url);
      var request = new RestRequest(Method.GET);
      request.AddHeader("Content-Type", "application/json");
      IRestResponse response = client.Execute(request);
      JObject jsonResponse = JObject.Parse(response.Content);
      float temp = jsonResponse.GetValue("prices").First.First["e5"].ToObject<float>();
      if (this.unleadedFuelPriceInEuro < temp)
      {
        this.unleadedFuelPriceInEuroTrend = trendUp;
      }
      else
      {
        if (this.unleadedFuelPriceInEuro > temp)
        {
          this.unleadedFuelPriceInEuroTrend = trendDown;
        }
        else
        {
          this.unleadedFuelPriceInEuroTrend = trendEqual;
        }
      }
      this.unleadedFuelPriceInEuro = temp;
      temp = jsonResponse.GetValue("prices").First.First["e10"].ToObject<float>();
      if (this.unleadedBioFuelPriceInEuro < temp)
      {
        this.unleadedBioFuelPriceInEuroTrend = trendUp;
      }
      else
      {
        if (this.unleadedBioFuelPriceInEuro > temp)
        {
          this.unleadedBioFuelPriceInEuroTrend = trendDown;
        }
        else
        {
          this.unleadedBioFuelPriceInEuroTrend = trendEqual;
        }
      }
      this.unleadedBioFuelPriceInEuro = temp;
      temp = jsonResponse.GetValue("prices").First.First["diesel"].ToObject<float>();
      if (this.dieselFuelPriceInEuro < temp)
      {
        this.dieselFuelPriceInEuroTrend = trendUp;
      }
      else
      {
        if (this.dieselFuelPriceInEuro > temp)
        {
          this.dieselFuelPriceInEuroTrend = trendDown;
        }
        else
        {
          this.dieselFuelPriceInEuroTrend = trendEqual;
        }
      }
      this.dieselFuelPriceInEuro = temp;
    }

    private void getBitcoin()
    {
      var client = new RestClient("https://rest.coinapi.io/v1/exchangerate/BTC/EUR");
      var request = new RestRequest(Method.GET);
      request.AddHeader("Content-Type", "application/json");
      request.AddHeader("X-CoinAPI-Key", ConfigurationManager.AppSettings["infoAPIKeyBitcoin"]);
      IRestResponse response = client.Execute(request);
      JObject jsonResponse = JObject.Parse(response.Content);
      float temp = jsonResponse.GetValue("rate").ToObject<float>();
      if (this.bitcoinInEuro < temp)
      {
        this.bitcoinInEuroTrend = trendUp;
      }
      else
      {
        if (this.bitcoinInEuro > temp)
        {
          this.bitcoinInEuroTrend = trendDown;
        }
        else
        {
          this.bitcoinInEuroTrend = trendEqual;
        }
      }
      this.bitcoinInEuro = temp;
    }


  }
}
