<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/cases">
    <result>
      <xsl:for-each select="case">
        <case id="{@id}">
          <date><xsl:value-of select="a:build-date(xs:integer(@year), xs:integer(@month), xs:integer(@day))"/></date>
          <time><xsl:value-of select="a:build-time(xs:integer(@hour), xs:integer(@minute), xs:integer(@second))"/></time>
          <time-with-zone><xsl:value-of select="a:build-time(xs:integer(@hour), xs:integer(@minute), xs:integer(@second), string(@timezone))"/></time-with-zone>
          <year-month-duration><xsl:value-of select="a:build-duration(xs:integer(@years), xs:integer(@months))"/></year-month-duration>
          <day-time-duration><xsl:value-of select="a:build-duration(xs:integer(@days), xs:integer(@hours), xs:integer(@minutes), xs:integer(@seconds))"/></day-time-duration>
        </case>
      </xsl:for-each>
    </result>
  </xsl:template>
</xsl:stylesheet>
