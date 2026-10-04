/**
 * Core data model. Perception (MediaPipe, or synthetic data in tests) produces
 * `FrameObservation`s; everything downstream — tracking, event detection,
 * planning, placement — only consumes these types, so the models are swappable.
 *
 * All coordinates are normalized to the video frame: x,y in [0,1], origin top-left.
 */

export interface Point {
  x: number;
  y: number;
}

export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

/** MediaPipe Pose landmark indices we rely on. */
export const LM = {
  nose: 0,
  leftEye: 2,
  rightEye: 5,
  leftShoulder: 11,
  rightShoulder: 12,
  leftWrist: 15,
  rightWrist: 16,
  leftHip: 23,
  rightHip: 24,
  leftAnkle: 27,
  rightAnkle: 28,
} as const;

export interface Landmark extends Point {
  /** 0..1 likelihood the landmark is visible. */
  v: number;
}

/** Facial expression cues (subset of MediaPipe blendshapes), each 0..1. */
export interface FaceCues {
  smile: number;
  jawOpen: number;
  eyeWide: number;
  browUp: number;
  /** Face center, used to associate a face with a body. */
  center: Point;
}

export interface PersonObservation {
  /** 33 MediaPipe pose landmarks. */
  landmarks: Landmark[];
  box: Box;
  face?: FaceCues;
}

export interface ObjectObservation {
  label: string;
  score: number;
  box: Box;
}

export interface FrameObservation {
  /** Seconds from video start. */
  t: number;
  people: PersonObservation[];
  objects: ObjectObservation[];
  /** True when this frame starts a new shot (hard cut). */
  cut?: boolean;
  /**
   * How far the camera has moved the picture since the start of the shot
   * (normalized). Subtract from positions to get camera-stabilized ones.
   */
  camera?: Point;
}

/** A person observation associated with a stable, session-local track id. */
export interface TrackedPerson extends PersonObservation {
  id: string;
}

export interface TrackedFrame {
  t: number;
  people: TrackedPerson[];
  objects: ObjectObservation[];
  cut?: boolean;
  camera?: Point;
}

/** Per-frame audio loudness (RMS, 0..1), sampled at a fixed rate. */
export interface AudioEnvelope {
  hz: number;
  rms: Float32Array;
}

export type EventType =
  | 'surprise'
  | 'laugh'
  | 'celebrate'
  | 'facepalm'
  | 'fall'
  | 'jump'
  | 'suddenMotion'
  | 'snack'
  | 'phone'
  | 'pet'
  | 'enter'
  | 'leave'
  | 'awkward'
  | 'loud';

export interface SceneEvent {
  type: EventType;
  /** Moment the event peaks (the trigger), seconds. */
  t: number;
  /** 0..1 */
  confidence: number;
  /** Track id the event belongs to; absent for scene-level events. */
  personId?: string;
  /** Free-form magnitude (speed, loudness...), roughly 0..1. */
  magnitude?: number;
}

export type TimingMode = 'instant' | 'beat' | 'delayed';

/** A scheduled reaction: what fires, when, and who it follows. */
export interface PlannedReaction {
  id: string;
  event: SceneEvent;
  /** When the sticker becomes visible (event time + comedic delay). */
  start: number;
  duration: number;
  timing: TimingMode;
  delayMs: number;
  score: number;
}
