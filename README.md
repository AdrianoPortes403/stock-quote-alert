Aplicação de console em C# que monitora a cotação de um ativo da B3 e envia um alerta por e-mail quando o preço:

sobe acima do preço de referência para venda → e-mail recomendando venda;
cai abaixo do preço de referência para compra → e-mail recomendando compra.

As cotações são obtidas pela API brapi.dev e os e-mails são enviados via SMTP com a biblioteca MailKit.
