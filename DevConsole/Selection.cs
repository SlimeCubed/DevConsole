using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DevConsole.GameConsole;

namespace DevConsole
{
    // Helper methods for selecting objects from the console
    internal class Selection
    {
        public static IEnumerable<AbstractPhysicalObject> SelectAbstractObjects(RainWorldGame game, string arg)
        {
            // TODO:
            // distance=..x [matches objects less than x distance away from an anchor position]
            // type=x [matches objects or creatures of type x]
            // players [matches all players]
            // enemies [matches everything hostile to the player]
            // all [matches everything]

            var firstPlayer = game.Players.Count == 0 ? null : game.Players[0];

            // me (or empty): select player 1
            if (arg == "me" || string.IsNullOrEmpty(arg))
            {
                if (firstPlayer == null)
                {
                    WriteLine("Could not find any players to select!");
                    return new AbstractPhysicalObject[0];
                }
                else
                {
                    if (firstPlayer?.realizedObject is not Player)
                    {
                        WriteLine("Player 1 must be realized to select!");
                        return new AbstractPhysicalObject[0];
                    }
                    return new[] { firstPlayer };
                }
            }

            // none: select nothing
            else if (arg == "none")
            {
                return new AbstractPhysicalObject[0];
            }

            // room: select everything in a room
            else if (arg == "room")
            {
                var list = new List<AbstractPhysicalObject>();
                var room = SpawnRoom?.abstractRoom ?? firstPlayer?.Room;
                if (room == null)
                {
                    WriteLine("Could not find a room to target!");
                }
                else
                {
                    foreach (var entity in room.entities)
                    {
                        if (entity is AbstractPhysicalObject o && o.Room.index == room.index)
                        {
                            list.Add(o);
                        }
                    }
                }
                return list;
            }

            // default: select a creature by ID
            else
            {
                try
                {
                    var id = EntityID.FromString(arg);
                    var ret = FindAbstractObjectByID(game, firstPlayer?.Room, id);
                    if (ret == null)
                    {
                        WriteLine("Found no entities in this region with that ID!");
                        return new AbstractPhysicalObject[0];
                    }
                    return new[] { ret };
                }
                catch
                {
                    WriteLine("Failed to parse entity ID!");
                    return new AbstractPhysicalObject[0];
                }
            }
        }

        private static AbstractPhysicalObject FindAbstractObjectByID(RainWorldGame game, AbstractRoom startSearch, EntityID id)
        {
            if (startSearch != null)
                foreach (var entity in startSearch.entities)
                    if (entity is AbstractPhysicalObject abstractPhysicalObject && id == entity.ID)
                        return abstractPhysicalObject;

            foreach (var room in game.world.abstractRooms)
                if (room != null && room != startSearch)
                    foreach (var entity in room.entities)
                        if (entity is AbstractPhysicalObject abstractPhysicalObject && id == entity.ID)
                            return abstractPhysicalObject;

            return null;
        }
    }
}
