MINI BAR stocks
Follow my https://submarine1234.itch.io/
Stock Ticker Bar

    Platform Support: Fully compatible with Raspberry Pi 5.
    Customization: You can easily resize the bar and add stocks. To add multiple tickers, separate them with a comma AAPL, TSLA, RTX
    Update Frequency: Data refreshes automatically approximately every minute.
    Powered by: YahooFinanceApi.
    Licensing: For those interested in a commercial version or a paid custom build, you are welcome to modify the code accordingly. 

Publishing Commands
Linux
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

Windows 10 / 11
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
<img width="347" height="52" alt="y2R7SU" src="https://github.com/user-attachments/assets/c4cb243e-5149-4f91-a396-aca849d7e96a" />
