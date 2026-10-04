using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;


    class StockQuoteAlert
    {
        static readonly HttpClient client = new HttpClient();

        public class Configuracao
        {
            public string Destinatario { get; set; } = "";
            public string Host { get; set; } = "";
            public int Porta { get; set; } = 587;
            public string Usuario { get; set; } = "";
            public string Senha { get; set; } = "";
            public string Remetente { get; set; } = "";
            public string Token { get; set; } = "";
            public int IntervaloSegundos { get; set; } = 60;
        }

        public static Configuracao Carregar()
        {
            // Procura o arquivo na pasta
            string caminho = Path.Combine(AppContext.BaseDirectory, "template.json");

            if (!File.Exists(caminho))
            {
                throw new FileNotFoundException($"Arquivo de configuração não encontrado: {caminho}");
            }

            string json = File.ReadAllText(caminho);
            Configuracao? config = JsonSerializer.Deserialize<Configuracao>(json);

            if (config == null)
            {
                throw new InvalidOperationException("O arquivo de configuração está vazio.");
            }

            return config;
        }

        static async Task Enviar_email(Configuracao configuracao, string assunto, string corpo)
        {
            var mensagem = new MimeMessage();
            mensagem.From.Add(new MailboxAddress("Stock Quote Alert", configuracao.Remetente));
            mensagem.To.Add(MailboxAddress.Parse(configuracao.Destinatario));
            mensagem.Subject = assunto;
            mensagem.Body = new TextPart("plain") { Text = corpo };

            
            var seguranca = configuracao.Porta == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(configuracao.Host, configuracao.Porta, seguranca);
            await cliente.AuthenticateAsync(configuracao.Usuario, configuracao.Senha);
            await cliente.SendAsync(mensagem);
            await cliente.DisconnectAsync(true);
        }

        static async Task<decimal> Pegar_preco_acao(string nome_da_acao, string token = "")
        {
            string url = $"https://brapi.dev/api/quote/{nome_da_acao}";
            if (!string.IsNullOrEmpty(token))
            {
                url += $"?token={token}";
            } 

            try
            {
                string json = await client.GetStringAsync(url);
                decimal? preco = JsonNode.Parse(json)?["results"]?[0]?["regularMarketPrice"]?.GetValue<decimal>();
                return preco ?? -1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erro: {e.Message}");
                return -1;
            }
        }

        public static async Task Main (string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("São necessários 3 inputs: nome_da_ação, preço_de_venda, preço_de_compra");
                Console.WriteLine("Ex.: stock-quote-alert.exe PETR4 22.67 22.59");
                return;
            }

            string nome_da_acao = args[0].ToUpper();

            // TryParse não derruba o programa se o usuário digitar algo inválido
            if (!decimal.TryParse(args[1], CultureInfo.InvariantCulture, out decimal valor_venda) ||
                !decimal.TryParse(args[2], CultureInfo.InvariantCulture, out decimal valor_compra))
            {
                Console.WriteLine("Preços inválidos. Use ponto como separador decimal (ex.: 22.67).");
                return;
            }

            if (valor_venda <= valor_compra)
            {
                Console.WriteLine("O preço de venda deve ser maior que o preço de compra.");
                return;
            }

            Configuracao configuracao;
            try
            {
                configuracao = Carregar();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erro ao carregar a configuração: {e.Message}");
                return;
            }

            string token = configuracao.Token;
            int intervalo = configuracao.IntervaloSegundos;
            bool comprar = false;
            bool vender = false;

            Console.WriteLine($"Monitorando {nome_da_acao} - venda acima de R$ {valor_venda:F2} | compra abaixo de R$ {valor_compra:F2} | a cada {intervalo}s");

            while (true)
            {
                try
                {
                    var preco = await Pegar_preco_acao(nome_da_acao, token);

                    if (preco == -1)
                    {
                        Console.WriteLine("Erro ao obter o valor da ação. Tentando no próximo ciclo.");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {nome_da_acao}: R$ {preco:F2}");

                        if (preco < valor_compra && !comprar)
                        {
                            await Enviar_email(configuracao, $"Alerta de Compra - {nome_da_acao}", $"Ação {nome_da_acao} está com preço R$ {preco:F2} < {valor_compra:F2}. Ideal para compra!");
                            Console.WriteLine("O cliente foi avisado para comprar.");
                            comprar = true;
                        }

                        if (preco > valor_venda && !vender)
                        {
                            await Enviar_email(configuracao, $"Alerta de Venda - {nome_da_acao}", $"Ação {nome_da_acao} está com preço R$ {preco:F2} > {valor_venda:F2}. Ideal para venda!");
                            Console.WriteLine("O cliente foi avisado para vender.");
                            vender = true;
                        }

                        // Libera um novo alerta quando o preço volta para dentro da faixa
                        if (comprar && preco >= valor_compra)
                        {
                            comprar = false;
                        }

                        if (vender && preco <= valor_venda)
                        {
                            vender = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Erro: {e.Message}. Tentando no próximo ciclo.");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalo));
            }
        }
    }