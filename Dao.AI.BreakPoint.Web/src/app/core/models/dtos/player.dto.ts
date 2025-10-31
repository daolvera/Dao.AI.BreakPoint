import { PlayerType } from '../enums/player-type';

export interface PlayerDto extends CreatePlayerDto {
  id: number;
  createdAt: string;
  updatedAt: string;
  estimatedPlayerType: PlayerType;
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
  latestCoachingTips: string[];
  estimatedRating: number | null;
  bigServerScore: number;
  serveAndVolleyerScore: number;
  allCourtPlayerScore: number;
  attackingBaselinerScore: number;
  solidBaselinerScore: number;
  counterPuncherScore: number;
}
