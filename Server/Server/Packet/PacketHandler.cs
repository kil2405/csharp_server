using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.DB;
using Server.Game;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class PacketHandler
{
	public static void C_MoveHandler(PacketSession session, IMessage packet)
	{
		if (packet is not C_Move movePacket)
			return;
		if (session is not ClientSession clientSession)
			return;

        //Console.WriteLine($"C_Move({movePacket.PosInfo.PosX}, {movePacket.PosInfo.PosY})");

		Player player = clientSession.MyPlayer;
		if (player == null)
			return;

		GameRoom room = player.Room;
		if (room == null)
			return;

		room.Push(room.HandleMove, player, movePacket);
	}

	public static void C_SkillHandler(PacketSession session, IMessage packet)
    {
		if (packet is not C_Skill skillPacket)
			return;
		if (session is not ClientSession clientSession)
			return;

		Player player = clientSession.MyPlayer;
		if (player == null)
			return;

		GameRoom room = player.Room;
		if (room == null)
			return;

		room.Push(room.HandleSkill, player, skillPacket);
	}

    public static void C_LoginHandler(PacketSession session, IMessage packet)
    {
        if (packet is not C_Login loginPacket)
			return;
		if (session is not ClientSession clientSession)
			return;

		clientSession.HandleLogin(loginPacket);
    }

	public static void C_EnterGameHandler(PacketSession session, IMessage packet)
	{
		if (packet is not C_EnterGame enterGamePacket)
			return;
		if (session is not ClientSession clientSession)
			return;

		clientSession.HandleEnterGame(enterGamePacket);
    }

    public static void C_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        if (packet is not C_CreatePlayer createPlayerPacket)
			return;
		if (session is not ClientSession clientSession)
			return;

        clientSession.HandleCreatePlayer(createPlayerPacket);
    }

    public static void C_EquipItemHandler(PacketSession session, IMessage packet)
    {
        if (packet is not C_EquipItem equipPacket)
			return;
		if (session is not ClientSession clientSession)
			return;

        Player player = clientSession.MyPlayer;
        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        room.Push(room.HandleEquipItem, player, equipPacket);
    }

	public static void C_PongHandler(PacketSession session, IMessage packet)
    {
        if (session is not ClientSession clientSession)
			return;

        clientSession.HandlePong();
    }
}
