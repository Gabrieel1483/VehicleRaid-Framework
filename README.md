# VehicleRaid-Framework
Here you will find the original framework and a test folder of what is currently available.
It's easy to create an incident raid. Leave notes in the XML files in the Test VehicleRaid/Defs/ folder. First, go to loadefvehicle.xml and load the vehicles you'll be using. I've already included some examples and notes. Simply decide whether you'll use the original vehicle with its configuration or create a modified vehicle for the raid. If you're going to create a custom vehicle for the raid, go to the VehicleNPC\BulldogNPC folder. I've already included notes to guide you in creating a custom raid vehicle.

Let's continue. Now go to the Defs\IncidentDefs folder, fill in the basic incident information, and you'll find the notes I've provided below.

And that's it! By following these steps and the notes I've provided, you'll be able to create an incident raid.

And if you want to add a raid strategy, go to Defs\RaidStrategyDefs, fill in the basic raid strategy settings, and in the <modExtensions> section, it's the same as for the incident, so you can use the same notes I left in the IncidentDefs.

And that's it, you have a raid strategy.

To add vehicles as defense to settlements, first go to Test VehicleRaid\Defs\SettlementVehicles. I already left a note in the XML file; it's basically the same as the incident file. Then go to Test VehicleRaid\Patches, use the example file, and you're done. It's not very difficult; just change the faction's "def".


Important: It is necessary to create a custom vehicle with a high cargo capacity, at least 3000 kg. Remember that the player can sell items to them and their weight will increase; it cannot exceed its limit. Also, keep in mind that if the maximum weight is low and it has many items that exceed the limit, it will not be able to take off or move, and the passengers will abandon the vehicle, leaving it at the colony base. The passengers themselves might even destroy the vehicle because in the Vehicle Framework, there is code that allows pawns to destroy the vehicle.

To create ground merchant vehicle caravans or aerial merchant vehicles (helicopters only), let’s start with ground vehicles. First, go to Defs\IncidentTradeVehicleDef; you will find two example XMLs. VRF_Incidents_Trader is simple and straightforward: just copy the code and change the defName, label, and baseChance.

Then, go to Patches. traderKind defines the items to be traded; you can create this from scratch or use the ones from the game. Basically, it is the TraderKindDef Base_Empire_Standard and it is linked to the Empire, so other factions cannot use it. <principalVehicle> is the vehicle that the caravan leader will arrive in. You already know the internal code, but there are two new tags:

    <tradeCargo> allows trading with items from the cargoitem.

    Inside cargoitem, there is another new tag, <tradeable>, which acts as a safeguard to prevent something from being sold.

    <cargoVehicles> also transport tradable items.

    <escortVehicles> are the guards and do not carry anything.

Now for aerial vehicles: only use helicopter-type vehicles, as I am unsure what happens with plane-type vehicles. We will only use one incident. Go to Defs\IncidentTradeVehicleDefs VRF_Incidents_Helicopter. It is the same process: change defName, label, and baseChance. The new parts are simple: vehicleKind is the vehicle, factionDef is the faction, and traderKind is the list of items to trade (TraderKindDef).

Once a merchant vehicle incident has been created, it will appear in the communications console to be requested or it will generate randomly; it will only appear once the colony becomes an ally with the faction you have set.

To make your helicopter fly, simply add the code found in VehicleRaid-Framework/VehicleRaid Framework MosquitoHover.xml
