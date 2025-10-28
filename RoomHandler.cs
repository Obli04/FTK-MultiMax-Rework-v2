namespace FTK_MultiMax_Rework {
    public static class RoomHandler {
        public static bool CreateRoom(ref GameLogic __instance, string _roomName, bool _isOpen) {
            PhotonNetwork.offlineMode = false;
            RoomOptions roomOptions = new RoomOptions();
            TypedLobby typedLobby = new TypedLobby();
            roomOptions.IsOpen = _isOpen;
            roomOptions.IsVisible = _isOpen;
            if (__instance.m_GameMode == GameLogic.GameMode.SinglePlayer) {
                roomOptions.MaxPlayers = 1;
            } else {
                roomOptions.MaxPlayers = (byte)GameFlowMC.gMaxPlayers;
            }
            typedLobby.Type = LobbyType.Default;
            PhotonNetwork.CreateRoom(_roomName, roomOptions, typedLobby);
            return false;
        }
    }
}
