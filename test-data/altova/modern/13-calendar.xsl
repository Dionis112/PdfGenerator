<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/cases">
    <result>
      <xsl:for-each select="case">
        <xsl:variable name="date" select="xs:date(@date)"/>
        <xsl:variable name="datetime" select="xs:dateTime(@datetime)"/>
        <case id="{@id}">
          <weekday-default><xsl:value-of select="a:weekday-from-date($date)"/></weekday-default>
          <weekday-sunday><xsl:value-of select="a:weekday-from-date($date, 0)"/></weekday-sunday>
          <weekday-monday><xsl:value-of select="a:weekday-from-date($date, 1)"/></weekday-monday>
          <week-default><xsl:value-of select="a:weeknumber-from-date($date)"/></week-default>
          <week-datetime-default><xsl:value-of select="a:weeknumber-from-dateTime($datetime)"/></week-datetime-default>
          <xsl:for-each select="0 to 2">
            <calendar code="{.}">
              <date><xsl:value-of select="a:weeknumber-from-date($date, .)"/></date>
              <datetime><xsl:value-of select="a:weeknumber-from-dateTime($datetime, .)"/></datetime>
            </calendar>
          </xsl:for-each>
        </case>
      </xsl:for-each>
    </result>
  </xsl:template>
</xsl:stylesheet>
