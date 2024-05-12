﻿Imports System.Drawing
Imports Mirage.Sharp.Asfw
Imports Core

Module C_Trade

#Region "Globals & Types"

    Friend InTrade As Integer
    Friend TradeX As Integer
    Friend TradeY As Integer
    Friend TheirWorth As String
    Friend YourWorth As String

#End Region

#Region "Incoming Packets"
    Sub Packet_TradeInvite(ByRef data() As Byte)
        Dim requester As Integer
        Dim buffer As New ByteStream(data)

        requester = buffer.ReadInt32
        Dialogue("Trade Invite", String.Format(Language.Trade.Request, Trim$((Player(requester).Name))), "", DialogueType.Trade, DialogueStyle.YesNo)

        buffer.Dispose()
    End Sub

    Sub Packet_Trade(ByRef data() As Byte)
        Dim buffer As New ByteStream(data)

        InTrade = buffer.ReadInt32

        ShowTrade()

        buffer.Dispose()
    End Sub

    Sub Packet_CloseTrade(ByRef data() As Byte)
        InTrade = 0
        HideWindow(GetWindowIndex("winTrade"))
    End Sub

    Sub Packet_TradeUpdate(ByRef data() As Byte)
        Dim datatype As Integer
        Dim buffer As New ByteStream(data)
        datatype = buffer.ReadInt32

        If dataType = 0 Then ' ours!
            For i = 1 To MAX_INV
                TradeYourOffer(i).Num = buffer.ReadInt32
                TradeYourOffer(i).Value = buffer.ReadInt32
            Next
            yourWorth = buffer.ReadInt32
            Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblYourValue")).text = yourWorth & "g"
        ElseIf dataType = 1 Then 'theirs
            For i = 1 To MAX_INV
                TradeTheirOffer(i).Num = buffer.ReadInt32
                TradeTheirOffer(i).Value = buffer.ReadInt32
            Next
            theirWorth = buffer.ReadInt32
            Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblTheirValue")).text = theirWorth & "g"
        End If

        buffer.Dispose()
    End Sub

    Sub Packet_TradeStatus(ByRef data() As Byte)
        Dim tradestatus As Integer
        Dim buffer As New ByteStream(data)

        tradestatus = buffer.ReadInt32

        Select Case tradeStatus
            Case 0 ' clear
                Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblStatus")).text = "Choose items to offer."
            Case 1 ' they've accepted
                Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblStatus")).text = "Other player has accepted."
            Case 2 ' you've accepted
                Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblStatus")).text = "Waiting for other player to accept."
            Case 3 ' no room
                Windows(GetWindowIndex("winTrade")).Controls(GetControlIndex("winTrade", "lblStatus")).text = "Not enough inventory space."
        End Select

        buffer.Dispose()
    End Sub

#End Region

#Region "Outgoing Packets"

    Friend Sub SendAcceptTrade()
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CAcceptTrade)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

    Friend Sub SendDeclineTrade()
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CDeclineTrade)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

    Sub SendTradeRequest(name As String)
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CTradeInvite)
        buffer.WriteString(name)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()

    End Sub

    Sub SendHandleTradeInvite(answer As Byte)
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CHandleTradeInvite)
        buffer.WriteInt32(answer)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()

    End Sub

    Friend Sub TradeItem(invslot As Integer, amount As Integer)
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CTradeItem)
        buffer.WriteInt32(invslot)
        buffer.WriteInt32(amount)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

    Friend Sub UntradeItem(invslot As Integer)
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CUntradeItem)
        buffer.WriteInt32(invslot)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

#End Region

End Module