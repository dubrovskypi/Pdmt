import { z } from "zod";

export const WebAuthResultSchema = z.object({
  accessToken: z.string(),
  accessTokenExpiresAt: z.string(),
});

export const TagResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  createdAt: z.string(),
  eventCount: z.number(),
});

export const EventResponseSchema = z.object({
  id: z.string(),
  timestamp: z.string(),
  type: z.number(),
  intensity: z.number(),
  title: z.string(),
  description: z.string().nullable(),
  context: z.string().nullable(),
  canInfluence: z.boolean(),
  tags: z.array(TagResponseSchema),
});
