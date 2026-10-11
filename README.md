# EmlViewer

A tiny Windows app for opening `.eml` email files.

## Install

1. Build the exe with the .NET 10 SDK:
   ```
   dotnet publish -c Release
   ```
2. Put `EmlViewer.exe` anywhere on your PC. No installer, no setup.
3. Run `EmlViewer.exe` to make it the default app for `.eml` files. The window walks you through it: click **Register**, then **Open Default apps**, and pick EmlViewer for `.eml` in Windows Settings.

## Use

Double-click an `.eml` file to view it.

If Windows doesn't open it with EmlViewer automatically, right-click → **Open with** → EmlViewer.

## Privacy

HTML emails often include tracking pixels and remote images that tell the sender you opened the message. EmlViewer blocks all of that by default and shows a banner when it does. Links still work — clicking one opens it in your normal web browser.
