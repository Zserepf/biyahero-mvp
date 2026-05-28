import { getRequestConfig } from "next-intl/server";
import { i18nConfig } from "../i18n.config";

/**
 * next-intl server request configuration.
 * Loads messages for the default locale on the server side.
 * The actual locale switching happens client-side via the NextIntlClientProvider.
 */
export default getRequestConfig(async () => {
  const locale = i18nConfig.defaultLocale;

  return {
    locale,
    messages: (await import(`../locales/${locale}.json`)).default,
  };
});
