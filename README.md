# Welcome to op.io

# Local setup instructions

* Clone the repo to your client using Git, or download the zip file and extract it to your desired location
* From Windows, download sqlite-tools-win-x64-3490100.zip in the following link: https://www.sqlite.org/download.html
* Move sqlite to your C drive
* Add the sqlite folder to your system environmental variables PATH
* To initialize an uninitialized database, do:
  > sqlite3 C:\...\Data\GameData.db
  > .read C:\...\Data\InitDatabase.sql
* Download sqlitebrowser from https://sqlitebrowser.org/dl/ to visualize the tables
* Launch DB Browser for SQLite then `Open Database` and select Data/GameData.db

# To run
`dotnet run`

# To revert to latest
git reset --hard HEAD

# To remove excess files no longer in the repo
git clean -fd

# Other
To unbind a key, make sure it's saved as an empty string.
