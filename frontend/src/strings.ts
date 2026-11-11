export const t = {
  idPlaceholder: "১৭ অঙ্কের জন্ম নিবন্ধন নম্বর",
  idAria: "জন্ম নিবন্ধন নম্বর",
  idHintRequired: "আবেদন করতে নম্বরটি দিন",
  idHintOk: "নম্বর গ্রহণ করা হয়েছে",
  idHintInvalid: (n: number) => `${n}/১৭ অঙ্ক। ঠিক ১৭ অঙ্ক দিতে হবে।`,
  retrying: "আমরা নিজে থেকেই আবার চেষ্টা করব।",

  createClassTitle: "Class তৈরি (স্কুল অ্যাডমিন)",
  classNamePlaceholder: "যেমন: Class 6 - Morning Shift",
  seatsPlaceholder: "Seat সংখ্যা",
  create: "তৈরি করুন",
  creating: "তৈরি হচ্ছে...",
  enterClassName: "Class-এর নাম লিখুন।",
  seatsInvalid: "Seat সংখ্যা ১ থেকে ১,০০,০০০-এর মধ্যে একটি পূর্ণ সংখ্যা হতে হবে।",
  classCreated: (n: string) => `"${n}" class তৈরি হয়েছে।`,

  classListTitle: "Class তালিকা",
  needId: "আবেদন করতে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।",
  loadingClasses: "Class-এর তালিকা আসছে...",
  noClasses: "এখনও কোনো class নেই। উপরে থেকে একটি তৈরি করুন।",
  colClass: "Class",
  colTotal: "মোট",
  colHeld: "Hold-এ",
  colConfirmed: "নিশ্চিত",
  colAvailable: "খালি",
  apply: "আবেদন করুন",
  applying: "আবেদন হচ্ছে...",
  alreadyApplied: "আবেদন করা হয়েছে",
  full: "Seat শেষ",
  applied: (n: string) => `"${n}"-এ seat hold করা হয়েছে। সময় শেষ হওয়ার আগে ভর্তি ফি দিন।`,

  myAppsTitle: "আমার আবেদন",
  myAppsNeedId: "আপনার আবেদন দেখতে সঠিক জন্ম নিবন্ধন নম্বর লিখুন।",
  loading: "আসছে...",
  noApps: "আপনার কোনো আবেদন নেই।",
  timeLeft: "ফি দেওয়ার বাকি সময়:",
  pay: "ভর্তি ফি দিন",
  payBusy: "অপেক্ষা করুন...",
  cancel: "বাতিল",
  paidNote: "ভর্তি ফি জমা হয়েছে। Seat নিশ্চিত।",
  expiredNote: "সময় শেষ। Seat ছেড়ে দেওয়া হয়েছে।",
  paid: (n: string) => `"${n}"-এ ভর্তি নিশ্চিত হয়েছে।`,
  cancelled: "আবেদন বাতিল করা হয়েছে।",

  unreachable: "Server-এর সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট দেখে আবার চেষ্টা করুন।",
  genericError: "কিছু একটা সমস্যা হয়েছে। আবার চেষ্টা করুন।",
};

// Bangla digits (০-৯) typed into the ID box become normal digits.
export function normalizeDigits(s: string): string {
  return s.replace(/[০-৯]/g, (d) => String("০১২৩৪৫৬৭৮৯".indexOf(d)));
}

// The backend replies in English. These are shown to the user in Bangla.
const errorMap: Record<string, string> = {
  "Class name is required.": "Class-এর নাম লিখুন।",
  "Class name is too long (max 200 characters).": "Class-এর নাম অনেক বড় (সর্বোচ্চ ২০০ অক্ষর)।",
  "Total seats must be a whole number between 1 and 100000.":
    "Seat সংখ্যা ১ থেকে ১,০০,০০০-এর মধ্যে একটি পূর্ণ সংখ্যা হতে হবে।",
  "Class not found.": "Class পাওয়া যায়নি।",
  "You already have an active application for this class.":
    "এই class-এ আপনার একটি আবেদন আগে থেকেই চালু আছে।",
  "Sorry, no seats are available in this class.": "দুঃখিত, এই class-এ আর কোনো খালি seat নেই।",
  "Application not found.": "আবেদনটি পাওয়া যায়নি।",
  "This application belongs to a different student.": "এই আবেদনটি অন্য শিক্ষার্থীর।",
  "This application was cancelled and cannot be confirmed.": "আবেদনটি বাতিল হয়ে গেছে, তাই নিশ্চিত করা যাবে না।",
  "This application has expired. Please apply again.": "আবেদনের মেয়াদ শেষ হয়ে গেছে। আবার আবেদন করুন।",
  "A confirmed admission cannot be cancelled.": "নিশ্চিত হওয়া ভর্তি বাতিল করা যায় না।",
  "This application has already expired.": "আবেদনের মেয়াদ আগেই শেষ হয়ে গেছে।",
  "Student ID (Birth Registration Number) must be exactly 17 digits.":
    "জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে।",
};

export function translateError(msg: string | undefined, status: number): string {
  if (msg && errorMap[msg]) return errorMap[msg];
  if (status === 400) return "ঠিকমতো তথ্য দিয়ে আবার চেষ্টা করুন।";
  if (status === 403) return "এই কাজটি করার অনুমতি আপনার নেই।";
  if (status === 404) return "খুঁজছেন এমন কিছু পাওয়া যায়নি।";
  if (status === 409) return "এখন এটি করা সম্ভব নয়। Seat শেষ হয়ে যেতে পারে বা আবেদনের মেয়াদ শেষ হতে পারে।";
  return "Server-এ সমস্যা হয়েছে। আবার চেষ্টা করুন।";
}