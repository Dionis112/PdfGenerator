<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/cases">
    <result>
      <xsl:for-each select="case">
        <xsl:variable name="value" select="xs:dateTime(@value)"/>
        <xsl:variable name="delta" select="xs:integer(@delta)"/>
        <case id="{@id}" input="{@value}" delta="{@delta}">
          <years><xsl:value-of select="a:add-years-to-dateTime($value, $delta)"/></years>
          <months><xsl:value-of select="a:add-months-to-dateTime($value, $delta)"/></months>
          <days><xsl:value-of select="a:add-days-to-dateTime($value, $delta)"/></days>
          <hours><xsl:value-of select="a:add-hours-to-dateTime($value, $delta)"/></hours>
          <minutes><xsl:value-of select="a:add-minutes-to-dateTime($value, $delta)"/></minutes>
          <seconds><xsl:value-of select="a:add-seconds-to-dateTime($value, $delta)"/></seconds>
        </case>
      </xsl:for-each>
    </result>
  </xsl:template>
</xsl:stylesheet>
