export interface UserActivityDay {
  date: string;
  count: number;
}

export interface AcceptedProblem {
  id: string;
  problemNumber: number;
  title: string;
  difficulty: 'Easy' | 'Medium' | 'Hard' | string;
  acceptedAt: string;
}

export interface UserStats {
  totalSolved: number;
  easySolved: number;
  mediumSolved: number;
  hardSolved: number;
  currentStreak: number;
  activity: UserActivityDay[];
  acceptedProblems: AcceptedProblem[];
}

export interface UserProfile {
  id: string;
  username: string;
  email: string;
  displayName: string | null;
  bio: string | null;
  profilePictureUrl: string | null;
  createdAt: string;
  stats: UserStats;
  preferredLanguage: string | null;
  layoutState: string | null;
}

export interface UserProfileUpdateRequest {
  displayName: string | null;
  bio: string | null;
}

export interface UserPreferencesUpdateRequest {
  preferredLanguage?: string | null;
  layoutState?: string | null;
}

export interface UserSettingsUpdateRequest {
  username?: string;
  email?: string;
  currentPassword?: string;
  newPassword?: string;
}
