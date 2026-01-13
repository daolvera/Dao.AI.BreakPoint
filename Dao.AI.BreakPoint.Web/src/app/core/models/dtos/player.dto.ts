import { Handedness } from '../enums/handedness.enum';
import { PlayerType } from '../enums/player-type.enum';

export interface PlayerDto extends CreatePlayerDto {
  id: number;
  createdAt: string;
  updatedAt: string;
  estimatedPlayerType: PlayerType;
  handedness: Handedness;
}

export interface CreatePlayerDto {
  name: string;
  email: string | null;
}

export interface PlayerWithStatsDto extends PlayerDto {
  totalMatches: number;
  matchesWon: number;
  matchesLost: number;
  winPercentage: number;
  estimatedRating: number | null;
  bigServerScore: number;
  serveAndVolleyerScore: number;
  allCourtPlayerScore: number;
  attackingBaselinerScore: number;
  solidBaselinerScore: number;
  counterPuncherScore: number;
  /** AI-generated summary of the player's training history and progression */
  trainingHistorySummary: string | null;
}
