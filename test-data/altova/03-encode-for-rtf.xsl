<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:altova="http://www.altova.com/xslt-extensions" exclude-result-prefixes="altova">
  <xsl:output method="xml" encoding="utf-8" indent="no"/>
  <xsl:template match="/samples">
    <result>
      <xsl:for-each select="text">
        <sample id="{@id}">
          <encoded whitespace="true" newlines="true"><xsl:value-of select="altova:encode-for-rtf(string(.), true(), true())"/></encoded>
          <encoded whitespace="true" newlines="false"><xsl:value-of select="altova:encode-for-rtf(string(.), true(), false())"/></encoded>
          <encoded whitespace="false" newlines="true"><xsl:value-of select="altova:encode-for-rtf(string(.), false(), true())"/></encoded>
          <encoded whitespace="false" newlines="false"><xsl:value-of select="altova:encode-for-rtf(string(.), false(), false())"/></encoded>
        </sample>
      </xsl:for-each>
      <absent><xsl:value-of select="altova:encode-for-rtf((), true(), true())"/></absent>
    </result>
  </xsl:template>
</xsl:stylesheet>
