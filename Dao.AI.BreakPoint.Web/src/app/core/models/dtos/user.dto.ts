export interface UserDto {
  id: number;
  email: string;
  name: string;
  isProfileComplete: boolean;
  playerId: number | null;
}
