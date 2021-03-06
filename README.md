# BattleShip


**Database migrations**

~~~
dotnet ef migrations add InitialMigration --project DAL --startup-project ConsoleApp
dotnet ef database update  --project DAL --startup-project ConsoleApp
~~~


**Database droping**

~~~
dotnet ef database drop  --project DAL --startup-project ConsoleApp
~~~


## Project images

**Main Menu**

![picture](Images/MainMenuEmpty.png)


**Create a new Game**

![picture](Images/10x7CreateGame.png)

**Player named "Bot1" placing his boats**

![picture](Images/Bot1PlacingBoats10x7.png)

**Gameplay**

![picture](Images/Ingame10x7.png)


**Main Menu with games**

![picture](Images/MainMenuWithGame.png)
