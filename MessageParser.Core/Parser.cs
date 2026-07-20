using System;
using System.Collections.Generic;
using System.Linq;

namespace MessageParser.Core
{
    public static class Parser
    {
        public static ParsedMessage Parse(string rawMessage)
        {
            var result = new ParsedMessage
            {
                RawMessage = rawMessage,
                IsValid = false
            };

            if (string.IsNullOrWhiteSpace(rawMessage))
            {
                result.Error = "Message is empty.";
                return result;
            }

            try
            {
                // Expected format: SENDER -> RECEIVER | COMMAND | KEY1=VAL1;KEY2=VAL2;...
                var parts = rawMessage.Split('|');
                if (parts.Length < 2)
                {
                    result.Error = "Invalid format. Expected at least SENDER -> RECEIVER | COMMAND";
                    return result;
                }

                // 1. Parse Routing (Sender -> Receiver)
                var routingPart = parts[0].Trim();
                var routingSplit = routingPart.Split(new[] { "->" }, StringSplitOptions.None);
                if (routingSplit.Length != 2)
                {
                    result.Error = "Invalid routing format. Expected 'Sender -> Receiver'";
                    return result;
                }

                result.Sender = CleanRoutingNode(routingSplit[0]);
                result.Receiver = CleanRoutingNode(routingSplit[1]);

                if (string.IsNullOrEmpty(result.Sender) || string.IsNullOrEmpty(result.Receiver))
                {
                    result.Error = "Sender and Receiver must not be empty.";
                    return result;
                }

                // 2. Parse Command
                result.Command = parts[1].Trim();
                if (string.IsNullOrEmpty(result.Command))
                {
                    result.Error = "Command must not be empty.";
                    return result;
                }

                // 3. Parse Payload if present
                if (parts.Length > 2)
                {
                    var payloadPart = parts[2].Trim();
                    if (!string.IsNullOrEmpty(payloadPart))
                    {
                        var pairs = payloadPart.Split(';');
                        foreach (var pair in pairs)
                        {
                            if (string.IsNullOrWhiteSpace(pair)) continue;

                            var kvSplit = pair.Split(new[] { '=' }, 2);
                            if (kvSplit.Length == 2)
                            {
                                var key = kvSplit[0].Trim();
                                var value = kvSplit[1].Trim();
                                if (!string.IsNullOrEmpty(key))
                                {
                                    result.Payload[key] = value;
                                }
                            }
                            else
                            {
                                // Single key without value or bad format
                                var key = pair.Trim();
                                if (!string.IsNullOrEmpty(key))
                                {
                                    result.Payload[key] = string.Empty;
                                }
                            }
                        }
                    }
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.Error = $"Parsing exception: {ex.Message}";
            }

            return result;
        }

        private static string CleanRoutingNode(string node)
        {
            var cleaned = node.Trim();
            // Remove wrapping square brackets or parentheses if they exist
            if (cleaned.StartsWith('[') && cleaned.EndsWith(']'))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }
            else if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }
            return cleaned;
        }
    }
}
