// Bu dosya artık kullanılmıyor.
// Bot modu PlayerPrefs üzerinden ("BotMode" key) taşınıyor.
// NetworkBootstrap.StartVsBot() → PlayerPrefs.SetInt("BotMode",1)
// BotSpawner.OnNetworkSpawn()  → PlayerPrefs.GetInt("BotMode",0)
