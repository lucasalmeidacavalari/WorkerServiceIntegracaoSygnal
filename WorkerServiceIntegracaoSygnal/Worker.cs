using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using WorkerServiceIntegracaoSygnal.Util;
using WorkerServiceIntegracaoSygnal.Dominio;

namespace WorkerServiceIntegracaoSygnal
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IWebDriver _driver;
        private TimeSpan _horaParaAnaliseInicio;
        private TimeSpan _horaParaAnaliseFim;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🚀 Serviço iniciado.");

            _driver = SetupWebDriver();
            if (_driver == null)
            {
                _logger.LogError("⚠️ Nenhum navegador compatível encontrado.");
                return Task.CompletedTask;
            }

            _horaParaAnaliseInicio = TimeSpan.Parse(Config.GetAppSettings("HoraParaAnaliseInicio"));
            _horaParaAnaliseFim = TimeSpan.Parse(Config.GetAppSettings("HoraParaAnaliseFimInicio"));

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Serviço interrompido.");
            _driver?.Quit();
            _driver = null;
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan horaAtual = DateTime.Now.TimeOfDay;

                if (horaAtual >= _horaParaAnaliseInicio && horaAtual <= _horaParaAnaliseFim)
                {
                    bool dadosEncontrados = await Atualiza();

                    if (dadosEncontrados)
                    {
                        _logger.LogInformation("⏳ Aguardando 10 minutos antes da próxima execução...");
                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Nenhum dado encontrado. Tentando novamente agora...");
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Pequeno delay antes de tentar de novo
                    }
                }
                else
                {
                    _logger.LogInformation($"⏳ Fora do horário de operação ({_horaParaAnaliseInicio} - {_horaParaAnaliseFim}).");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); // Mantém o intervalo se estiver fora do horário
                }
            }
        }

        private async Task<bool> Atualiza()
        {
            var loginUrl = "https://www.sygnal.ai/account/sign-in.html";
            var targetUrl = "https://www.sygnal.ai/subscriptions/subscribed-signals.html?productserviceid=49&productname=PlatinumPulse%20by%20FIT&scid=cus_RxvuYjYykpDcEl";
            string email = "itasouza@yahoo.com.br";
            string password = "Root#0123";

            if (_driver == null)
            {
                _logger.LogError("❌ WebDriver não inicializado.");
                return false;
            }

            try
            {
                if (!CheckIfLoggedIn(targetUrl))
                {
                    _logger.LogInformation("🔑 Realizando login...");
                    _driver.Navigate().GoToUrl(loginUrl);
                    WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

                    wait.Until(d => d.FindElement(By.Id("signinForm")));

                    IWebElement emailField = _driver.FindElement(By.Id("inputEmail"));
                    IWebElement passwordField = _driver.FindElement(By.Id("inputPassword"));
                    IWebElement loginButton = _driver.FindElement(By.Id("signInBtn"));

                    emailField.Clear();
                    emailField.SendKeys(email);
                    passwordField.Clear();
                    passwordField.SendKeys(password);
                    loginButton.Click();

                    _logger.LogInformation("⏳ Aguardando login ser processado...");
                    await Task.Delay(5000);

                    try
                    {
                        IWebElement errorMessage = _driver.FindElement(By.Id("failedLogin"));
                        if (errorMessage.Displayed)
                        {
                            _logger.LogError("❌ Login falhou. Verifique as credenciais.");
                            return false;
                        }
                    }
                    catch (NoSuchElementException) { }

                    if (!CheckIfLoggedIn(targetUrl))
                    {
                        _logger.LogError("❌ Login falhou mesmo sem mensagem de erro.");
                        return false;
                    }
                }

                _logger.LogInformation("✅ Login confirmado, acessando os dados...");
                _driver.Navigate().GoToUrl(targetUrl);
                await Task.Delay(3000);

                var forexDataList = ExtractTableData();

                if (forexDataList.Any())
                {
                    _logger.LogInformation("✅ Dados extraídos com sucesso:");
                    foreach (var data in forexDataList)
                    {
                        _logger.LogInformation(data.ToString());
                    }
                    return true; // ✅ Dados encontrados
                }
                else
                {
                    _logger.LogWarning("⚠️ Nenhum dado encontrado na tabela.");
                    return false; // ❌ Nenhum dado encontrado
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Erro ao atualizar: {ex.Message}");
                return false;
            }
        }

        private List<Sygnal> ExtractTableData()
        {
            List<Sygnal> forexDataList = new List<Sygnal>();

            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));

                // 🔹 Aguarda a tabela ser carregada
                IWebElement table = wait.Until(d => d.FindElement(By.Id("watchtable")));
                IWebElement tbody = wait.Until(d => table.FindElement(By.TagName("tbody")));

                var rows = tbody.FindElements(By.TagName("tr"));

                if (rows.Count == 0)
                {
                    _logger.LogWarning("⚠️ Nenhuma linha encontrada na tabela.");
                }

                // 🔹 Carrega os símbolos da configuração
                var listaDeSymbolos = Config.GetAppSettings("ListaDeSymbolos")?.Split(';').ToList() ?? new List<string>();

                foreach (var row in rows)
                {
                    var columns = row.FindElements(By.TagName("td"));
                    if (columns.Count < 6) continue;

                    // 🔹 Captura o nome e símbolo
                    var symbolElements = columns[2].FindElements(By.TagName("p"));
                    if (symbolElements.Count < 2) continue;

                    string name = symbolElements[0].Text.Trim();
                    string symbol = symbolElements[1].Text.Trim();

                    if (!listaDeSymbolos.Contains(symbol)) continue;

                    // 🔹 Modelo
                    string model = columns[3].FindElement(By.TagName("a")).Text.Trim();

                    // 🔹 Signal (valor principal e secundário)
                    var signalElements = columns[4].FindElements(By.TagName("p"));
                    string signalValue = signalElements[0].Text.Trim();
                    string signalText = signalElements.Count > 1 ? signalElements[1].Text.Trim() : "";

                    // 🔹 Previous (valor principal e secundário)
                    var previousElements = columns[5].FindElements(By.TagName("p"));
                    string previousValue = previousElements.Count > 0 ? previousElements[0].Text.Trim() : "";
                    string previous7d = previousElements.Count > 1 ? previousElements[1].Text.Trim() : "";

                    // 🔹 Última atualização
                    string updated = columns[7].FindElement(By.TagName("p")).Text.Trim();

                    forexDataList.Add(new Sygnal(symbol, name, model, signalValue, signalText, previousValue, previous7d, updated));
                }

                _logger.LogInformation($"✅ Extraídos {forexDataList.Count} sinais com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Erro ao processar tabela: {ex.Message}");
            }

            return forexDataList;
        }


        private static IWebDriver SetupWebDriver()
        {
            try
            {
                ChromeOptions options = new ChromeOptions();
                options.AddArgument("--headless");
                return new ChromeDriver(options);
            }
            catch (Exception) { Console.WriteLine("Chrome não encontrado."); }

            try
            {
                EdgeOptions options = new EdgeOptions();
                options.AddArgument("--headless");
                return new EdgeDriver(options);
            }
            catch (Exception) { Console.WriteLine("Edge não encontrado."); }

            try
            {
                FirefoxOptions options = new FirefoxOptions();
                options.AddArgument("--headless");
                return new FirefoxDriver(options);
            }
            catch (Exception) { Console.WriteLine("Firefox não encontrado."); }

            return null;
        }

        private bool CheckIfLoggedIn(string targetUrl)
        {
            try
            {
                _driver.Navigate().GoToUrl(targetUrl);

                // 🔹 Aguarda o carregamento da página
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                IWebElement signRegBtns = wait.Until(d => d.FindElement(By.Id("signRegBtns")));

                // 🔹 Obtém a classe do elemento
                string classAttribute = signRegBtns.GetAttribute("class");

                if (classAttribute.Contains("d-block"))
                {
                    _logger.LogWarning("⚠️ Usuário NÃO está logado.");
                    return false;
                }

                _logger.LogInformation("✅ Usuário está logado.");
                return true;
            }
            catch (NoSuchElementException)
            {
                _logger.LogError("❌ Elemento signRegBtns não encontrado. Pode indicar erro de carregamento da página.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Erro ao verificar sessão: {ex.Message}");
                return false;
            }
        }
    }

}
