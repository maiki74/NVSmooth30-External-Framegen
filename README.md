# NVSmooth30 External Framegen

Aplicativo Windows com interface para usar o NVSmooth30 em um processo separado do jogo.

**Este é um projeto e repositório independentes. O projeto NVSmooth30-Bridge-App permanece inalterado.** A base inicial deste novo projeto é o código estável desenvolvido para o mesmo proprietário, com armazenamento próprio de configurações e arquivos temporários.

![Interface em português](docs/interface.png)

## Baixar e usar

[**Baixar NVSmooth30.exe**](https://raw.githubusercontent.com/maiki74/NVSmooth30-External-Framegen/main/downloads/NVSmooth30.exe) · [Código-fonte em ZIP](https://github.com/maiki74/NVSmooth30-External-Framegen/archive/refs/heads/main.zip)

1. Abra o aplicativo.
2. Na primeira configuração, selecione o `version.dll` original do **NVSmooth-30 v0.1**, obtido na [página de ItsAdeline no Nexus Mods](https://www.nexusmods.com/site/mods/2344).
3. Abra o jogo normalmente, de preferência em janela sem bordas.
4. Selecione a janela e clique em **Iniciar**.
5. Volte ao jogo. Para desativar, use o botão **Desativar** ou **Ctrl + Alt + F10**.

A entrada especial do Nightreign pode abrir o jogo pela Steam. Se a Steam mostrar opções, selecione o jogo normal e clique em Jogar.

O executável inclui o motor de captura/apresentação deste projeto. O backend de terceiros é importado uma vez e preparado localmente; não é redistribuído neste repositório. Depois disso, fica salvo para as próximas aberturas. No computador de desenvolvimento, essa configuração já foi feita.

## O que faz

O jogo continua funcionando normalmente e recebendo seus comandos. O aplicativo captura apenas a janela escolhida, envia a imagem pela GPU a um auxiliar e usa o caminho de apresentação NVIDIA por meio do NVSmooth30. O resultado aparece sobre a janela original.

- Interface com **Iniciar**, **Desativar** e seleção de janela.
- Inglês, espanhol e português do Brasil, com preferências salvas.
- Opções para cursor e visibilidade da saída em gravações.
- Encerramento do auxiliar ao fechar o jogo ou o aplicativo.
- Registro local para diagnóstico.

O aplicativo não copia DLLs para a pasta do jogo. O componente NVSmooth30 é carregado no auxiliar `SmoothHost.exe`.

## Requisitos e limites

- Windows x64 e .NET Framework 4.8.
- NVIDIA compatível com o experimento upstream; o desenvolvimento foi validado em uma **RTX 3050 8 GB**.
- Driver com o componente NvPresent e Smooth Motion habilitado para `SmoothHost.exe`. Se necessário, configure pelo [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector).
- O backend aceito tem versão e hashes específicos: veja [BACKEND.md](docs/BACKEND.md).

O número na interface é a taxa de imagens recebidas. Ele **não é multiplicado por dois** e não mede, sozinho, os quadros gerados que chegam à tela. O caminho original foi observado perto de 120 FPS exibidos em trechos com fonte a 60 FPS; isso não é uma promessa para qualquer cena ou jogo.

O autor original não oferece suporte a jogos protegidos por anticheat. Este projeto não implementa um bypass nem garante compatibilidade online. A inicialização e a observação pontual do Nightreign no computador do proprietário não certificam partidas prolongadas, outros drivers ou futuras atualizações.

Gravar 120 imagens distintas por segundo no OBS não foi validado. HDR, mudança de resolução durante a execução e todas as combinações de monitores também não foram validados.

## Compilar

```powershell
# PowerShell - raiz do projeto - Windows x64 / .NET Framework 4.8
.\build.ps1
.\dist\NVSmooth30.exe
```

Não é necessário baixar uma DLL para compilar. O executável público incorpora apenas o auxiliar do próprio projeto. A importação do backend ocorre na configuração local.

```powershell
# PowerShell - verificação do pacote - Windows x64
.\build.ps1 -IncludeTestFixture
.\tests\Test-Package.ps1 -BackendPath 'C:\SuaPasta\version.dll'
```

## Arquivos e configurações

O runtime, o backend preparado e as preferências ficam em:

```text
%LOCALAPPDATA%\NVSmooth30 External Framegen
```

Esse diretório é separado do utilizado pelo projeto anterior. Para conferir os registros, use **Abrir registros**.

## Fontes e créditos

- **ItsAdeline:** criador do NVSmooth-30, backend usado para compatibilidade. [Nexus Mods](https://www.nexusmods.com/site/mods/2344) · [código upstream](https://github.com/ItsAdeline/NVSmooth30).
- **NVIDIA:** driver, Smooth Motion e NvPresent, responsáveis pelo caminho de geração/apresentação usado.
- **Microsoft:** Windows Graphics Capture, Direct3D, DXGI e .NET.
- **maiki74:** ideia, direção e testes em jogos.
- **OpenAI Codex (GPT):** implementação e ajustes do código original do aplicativo, sob direção do proprietário. Isso não atribui a este projeto o backend de ItsAdeline nem a tecnologia NVIDIA.

A lista detalhada, com links das APIs e origem da correção binária, está em [FONTES.md](docs/FONTES.md). As condições dos componentes estão em [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Código original do projeto sob [licença MIT](LICENSE). Os direitos de componentes de terceiros permanecem com seus autores.
