import { getMondayOf, addDays, toDateString, formatWeekRange, formatShortDate, formatDayDisplay } from "./dateUtils";

describe("getMondayOf", () => {
  it("returns monday when given a wednesday", () => {
    const wed = new Date("2026-04-08T00:00:00Z");
    expect(toDateString(getMondayOf(wed))).toBe("2026-04-06");
  });

  it("returns same day when given a monday", () => {
    const mon = new Date("2026-04-06T00:00:00Z");
    expect(toDateString(getMondayOf(mon))).toBe("2026-04-06");
  });

  it("returns previous monday when given a sunday", () => {
    const sun = new Date("2026-04-12T00:00:00Z");
    expect(toDateString(getMondayOf(sun))).toBe("2026-04-06");
  });
});

describe("addDays", () => {
  it("adds positive days", () => {
    const d = new Date("2026-04-06T00:00:00Z");
    expect(toDateString(addDays(d, 6))).toBe("2026-04-12");
  });

  it("subtracts days when n is negative", () => {
    const d = new Date("2026-04-06T00:00:00Z");
    expect(toDateString(addDays(d, -7))).toBe("2026-03-30");
  });

  it("handles month boundary", () => {
    const d = new Date("2026-03-30T00:00:00Z");
    expect(toDateString(addDays(d, 3))).toBe("2026-04-02");
  });
});

describe("toDateString", () => {
  it("formats date as YYYY-MM-DD", () => {
    expect(toDateString(new Date("2026-04-07T15:30:00Z"))).toBe("2026-04-07");
  });

  it("pads single-digit month and day with zeros", () => {
    expect(toDateString(new Date("2026-01-05T00:00:00Z"))).toBe("2026-01-05");
  });
});

describe("formatWeekRange", () => {
  it("returns DD.MM–DD.MM spanning monday to sunday", () => {
    expect(formatWeekRange("2026-04-06T00:00:00Z")).toBe("06.04–12.04");
  });

  it("handles month boundary correctly", () => {
    expect(formatWeekRange("2026-03-30T00:00:00Z")).toBe("30.03–05.04");
  });
});

describe("formatShortDate", () => {
  it("returns DD.MM format", () => {
    expect(formatShortDate("2026-04-07T00:00:00Z")).toBe("07.04");
  });
});

describe("formatDayDisplay", () => {
  it("returns monday label for a monday", () => {
    const { dayName } = formatDayDisplay("2026-04-06T00:00:00Z");
    expect(dayName).toBe("Пн");
  });

  it("returns sunday label for a sunday", () => {
    const { dayName } = formatDayDisplay("2026-04-12T00:00:00Z");
    expect(dayName).toBe("Вс");
  });

  it("returns correct day number", () => {
    const { day } = formatDayDisplay("2026-04-07T00:00:00Z");
    expect(day).toBe("7");
  });
});
