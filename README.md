Aplicação de console em C# que monitora a cotação de um ativo da B3 e envia um alerta por e-mail quando o preço:

quando o preço sobe acima do preço de referência um e-mail recomendando a venda é enviado.

Ou

quando o preço cai abaixo do preço de referência um e-mail recomendando a compra é enviado.

As cotações são obtidas pela API brapi.dev e os e-mails são enviados via SMTP com a biblioteca MailKit.
