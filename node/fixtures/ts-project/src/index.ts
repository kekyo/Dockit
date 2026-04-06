/////////////////////////////////////////////////////////////////////////////////////////////////
//
// Dockit - An automatic Markdown documentation generator, fetch from .NET XML comment/metadata.
// Copyright (c) Kouji Matsui (@kekyo@mi.kekyo.net)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
/////////////////////////////////////////////////////////////////////////////////////////////////

/**
 * Represents an error payload.
 */
export interface ErrorInfo {
  /**
   * Error message text.
   */
  message: string;
}

/**
 * Represents a named lookup table.
 */
export interface NameMap {
  /**
   * Primary label.
   */
  label: string;

  /**
   * Indexed value.
   *
   * @returns Indexed value text.
   */
  [key: string]: string | number;

  /**
   * Formats an index key.
   *
   * @param key Key parameter.
   * @returns Formatted key.
   */
  format(key: string): string;
}

/**
 * Represents available states.
 */
export enum SampleState {
  /**
   * Idle state.
   */
  Idle = 0,

  /**
   * Busy state.
   *
   * @remarks Used while processing is active.
   */
  Busy = 1,
}

/**
 * Represents a union result.
 *
 * @typeParam TValue Stored value type.
 * @remarks Type alias remarks.
 */
export type Result<TValue> = TValue | ErrorInfo;

/**
 * Represents a notification context object.
 */
export type NotificationContext = {
  /**
   * Initializes notifications.
   *
   * @param channel Channel parameter.
   */
  initialize(channel: string): void;

  /**
   * Optional title text.
   */
  title?: string;

  /**
   * Shows a local message.
   *
   * @param message Message parameter.
   */
  show: (message: string) => void;
};

/**
 * Represents the current mode.
 */
export const currentMode: 'auto' | 'manual' = 'auto';

/**
 * Builds a result.
 *
 * @typeParam TValue - Result value type.
 * @param value - Value parameter.
 * @returns - Result instance.
 * @remarks Function remarks.
 * @example
 * const output = createResult("ok");
 * @see Result
 */
export const createResult = <TValue>(value: TValue): Result<TValue> => value;

/**
 * Represents a box.
 *
 * @typeParam TValue Stored value type.
 * @remarks Type remarks.
 * @example
 * const box = new Box("value");
 * @see NameMap
 */
export class Box<TValue> {
  /**
   * Backing value field.
   */
  public current: TValue;

  /**
   * Gets the sample state.
   */
  public readonly state: SampleState = SampleState.Idle;

  /**
   * Initializes a new box.
   *
   * @param value Value parameter.
   */
  public constructor(value: TValue) {
    this.current = value;
  }

  /**
   * Gets the current value.
   *
   * @remarks Property remarks.
   */
  public get value(): TValue {
    return this.current;
  }

  /**
   * Sets the current value.
   *
   * @param next Next value parameter.
   */
  public set value(next: TValue) {
    this.current = next;
  }

  /**
   * Formats the current value.
   *
   * @param prefix Prefix parameter.
   * @returns Formatted text.
   */
  public format(prefix: string): string;

  /**
   * Formats the current value with repetition count.
   *
   * @param prefix Prefix parameter.
   * @param count Count parameter.
   * @returns Formatted text.
   */
  public format(prefix: string, count: number): string;

  public format(prefix: string, count: number | undefined): string {
    return count === undefined
      ? `${prefix}:${String(this.current)}`
      : `${prefix}:${String(this.current)}:${count}`;
  }
}
