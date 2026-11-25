# Valley Keepsake para Android (experimental)

Projeto independente para Android. A pessoa escolhe a pasta `Mods`, a biblioteca de traduções e a biblioteca de configurações pelo seletor do Android. As escolhas são lembradas por permissões persistentes (bookmarks). Não há caminho fixo do SMAPI.

O aplicativo encontra os `manifest.json` instalados, guarda traduções `i18n` do idioma selecionado, reaplica traduções ausentes sem substituir as já instaladas, desfaz as cópias feitas na sessão e faz backup de `config.json` por `UniqueID`. Para restaurar, a pessoa pode escolher todos os mods ou manter o dedo sobre um mod com backup salvo e tocar nos demais para selecionar vários; o botão de restauração mostra quantos foram selecionados. Na restauração, ignora arquivos idênticos e guarda a versão atual antes de substituir uma configuração diferente. As bibliotecas usam a mesma estrutura de pastas das versões de desktop para essas operações. Em toda abertura, uma tela de entrada mostra os cinco Junimos animados, o crédito "made by NatrollEXE" e a escolha de português ou inglês antes de avançar. A paleta no canto superior direito da entrada permite escolher as cores dos cinco temas do desktop, sem as molduras grandes; as escolhas de idioma e tema ficam salvas junto com as pastas. A lista de mods tem colunas com divisórias que podem ser deslizadas horizontalmente.

O nome da pasta que guarda a configuração anterior a uma restauração vem da chave `Backups before restore` dos catálogos `Languages/en.json` e `Languages/pt-BR.json`, incluídos no APK. Pastas de backup já existentes mantêm seus nomes.

Cada backup novo guarda apenas o `config.json` na pasta do `UniqueID`. Arquivos `informacoes.json` criados por versões anteriores não são necessários para a leitura e permanecem intactos nas bibliotecas existentes.

Ainda não há correspondência aproximada para mods renomeados, importação de arquivos escolhidos avulsos, aplicação de mapas `content.json`, seleção de destino incerto, nem os temas completos do desktop. A leitura da pasta Mods e o armazenamento das traduções foram confirmados em um aparelho real; a aplicação e a restauração ainda precisam de testes nesse aparelho antes de publicar um release. Confira o mod selecionado antes de restaurar sua configuração.

No Android 11 ou mais novo, o seletor do sistema impede a seleção de pastas dentro de `Android/data` e `Android/obb`. Se o port do SMAPI guardar `Mods` nesses locais, outro app independente não consegue acessar essa pasta pelo seletor; o port precisa oferecer uma pasta compartilhada acessível.

## Compilação

Requer .NET 10, `dotnet workload install android`, Android SDK e JDK. Em Windows, depois de instalar as dependências:

```powershell
dotnet build Valley-Keepsake-Android/ValleyKeepsake.Android.csproj -c Release -p:AndroidSdkDirectory=C:\caminho\android-sdk -p:JavaSdkDirectory=C:\caminho\jdk
```

O APK gerado fica em `Valley-Keepsake-Android/bin/Release/net10.0-android/android-arm64/`.
