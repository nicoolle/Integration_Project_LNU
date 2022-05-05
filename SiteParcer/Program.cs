using DTO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace SiteParcer
{
    class Program
    {
        [Obsolete]
        public static IEnumerable<NewsDTO> Crawl()
        {

            string homeUrl = "https://www.bbc.com/news/world-60525350";

            //options skip errors
            ChromeOptions chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("--ignore-certificate-errors");
            chromeOptions.AddArgument("--ignore-certificate-errors-spki-list");
            chromeOptions.AddArgument("--ignore-ssl-errors");
            chromeOptions.AddArgument("test-type");
            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddArgument("--start-maximized");

            IWebDriver driver = new ChromeDriver(@"C:\", chromeOptions);
            driver.Navigate().GoToUrl(homeUrl);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.CssSelector(".nw-i-service-news")));

            //get all news
            var latestNewsContainer = driver.FindElement(By.XPath("/html/body/div[7]/div/div/div[3]"));
            var elements = latestNewsContainer.FindElements(By.ClassName("lx-stream-post__header-link"));
            var news = elements
            .Select(el => new NewsDTO
            {
                Title = el.FindElement(By.TagName("span")).Text,
                Url = el.GetAttribute("href")
            }).ToList();

            for (int i = 0; i < news.Count; i++)
            {
                var n = news[i];
                try
                {
                    driver.Navigate().GoToUrl(n.Url);

                    wait.Until(d => d.FindElement(By.Id("header-content")));
                    //wait.Until(ExpectedConditions.ElementExists(By.CssSelector(".pg-headline")));
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    n.ID = (i + 1).ToString();
                    var descriptionElement = driver
                        .FindElement(By.XPath("//*[@id=\"main-content\"]/div[5]/div/div[1]/article/div[2]/div/p/b"));
                    n.Description = descriptionElement.Text;

                    var dateElement = driver
                        .FindElement(By.XPath("//*[@id=\"main-content\"]/div[5]/div/div[1]/article/header/div[1]/dl/div[1]/dd/span/time"));
                    n.DateOfPublication = DateTime.Parse(dateElement.GetAttribute("datetime"));

                    var authorElement = driver
                        .FindElement(By.XPath("//*[@id=\"main-content\"]/div[5]/div/div[1]/article/header/p/span/strong"));
                    n.Author = authorElement.Text;
                    //n.DateOfPublication = DateTime.Parse(driver.FindElement(By.CssSelector("meta[name^=pubdate]")).GetAttribute("content"));
                }
                catch (Exception) { }
                yield return n;
            }
        }

            [Obsolete]
        static void Main(string[] args)
        {
            ConnectionFactory factory = new ConnectionFactory();

            //creds
            factory.UserName = "guest";
            factory.Password = "guest";
            factory.VirtualHost = "/";
            factory.HostName = "localhost";

            using (IConnection conn = factory.CreateConnection())
            using (var model = conn.CreateModel())
            {
                model.QueueDeclare("news", false, false, false, null);

                foreach (var x in Crawl())
                {
                    Console.WriteLine(x);

                    var properties = model.CreateBasicProperties();
                    properties.Persistent = true;

                    model.BasicPublish(
                        "",
                        "news",
                        basicProperties: properties,
                        body: BinaryConverter.ObjectToByteArray(
                                x
                            )
                        );
                }
            }
        }
    }
}