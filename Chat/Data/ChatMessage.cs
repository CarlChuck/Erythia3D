using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network-serializable structure representing a chat message
/// Integrates with the existing MMORPG architecture for area-based filtering
/// </summary>
[System.Serializable]
public struct ChatMessage : INetworkSerializable, IEquatable<ChatMessage>
{
    public ulong senderId;
    public FixedString128Bytes senderName;
    public FixedString512Bytes content;
    public ChatChannel channel;
    public ChatPriority priority;
    public float timestamp;
    public Vector3 senderPosition;
    public int areaId;
    public ulong messageId;

    /// <summary>
    /// Creates a new chat message
    /// </summary>
    public ChatMessage(ulong senderId, string senderName, string content, ChatChannel channel, 
                      Vector3 senderPosition, int areaId, ChatPriority priority = ChatPriority.Normal)
    {
        this.senderId = senderId;
        this.senderName = new FixedString128Bytes(senderName);
        this.content = new FixedString512Bytes(content);
        this.channel = channel;
        this.priority = priority;
        this.timestamp = Time.time;
        this.senderPosition = senderPosition;
        this.areaId = areaId;
        this.messageId = GenerateMessageId();
    }

    /// <summary>
    /// Network serialization implementation
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref senderId);
        serializer.SerializeValue(ref senderName);
        serializer.SerializeValue(ref content);
        serializer.SerializeValue(ref channel);
        serializer.SerializeValue(ref priority);
        serializer.SerializeValue(ref timestamp);
        serializer.SerializeValue(ref senderPosition);
        serializer.SerializeValue(ref areaId);
        serializer.SerializeValue(ref messageId);
    }

    /// <summary>
    /// Equality comparison for ChatMessage
    /// </summary>
    public bool Equals(ChatMessage other)
    {
        return messageId == other.messageId;
    }

    public override bool Equals(object obj)
    {
        return obj is ChatMessage other && Equals(other);
    }

    public override int GetHashCode()
    {
        return messageId.GetHashCode();
    }

    /// <summary>
    /// Generates a unique message ID based on timestamp and sender
    /// </summary>
    private static ulong GenerateMessageId()
    {
        return (ulong)(DateTime.UtcNow.Ticks ^ UnityEngine.Random.Range(0, int.MaxValue));
    }

    /// <summary>
    /// Validates if the message content is acceptable
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(content.ToString()) && 
               content.Length > 0 && 
               content.Length <= 512 &&
               !string.IsNullOrEmpty(senderName.ToString());
    }
}